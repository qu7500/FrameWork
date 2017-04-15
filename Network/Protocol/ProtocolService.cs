﻿using UnityEngine;
using System.Collections;
using System.Net.Sockets;
using System.Collections.Generic;
using System;
using System.Net;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

public class ProtocolService : INetworkInterface 
{
    private const int TYPE_string = 1;
    private const int TYPE_int32 = 2;
    private const int TYPE_double = 3;
    private const int TYPE_bool = 4;
    private const int TYPE_custom = 5;
    private const int RT_repeated = 1;
    private const int RT_equired = 0;

    const string m_ProtocolFileName = "ProtocolInfo";
    const string m_methodNameInfoFileName = "MethodInfo";


    Dictionary<string, List<Dictionary<string, object>>> m_protocolInfo;

    Dictionary<int, string> m_methodNameInfo;
    Dictionary<string, int> m_methodIndexInfo;

    private Socket m_Socket;
    private byte[] m_readData;

    AsyncCallback m_acb = null;
    

    private Thread m_connThread;

    public override void Init()
    {
        ReadProtocolInfo();
        ReadMethodNameInfo();

        InitMessagePool(50);
        m_acb = new AsyncCallback(EndReceive);
        m_readData = new byte[102400];
        m_messageBuffer = new byte[204800];

        m_head = 0;
        m_total = 0;
    }

    public override void GetIPAddress()
    {
        m_IPaddress = "192.168.0.10";
        m_port = 7001;
    }
    public override void SetIPAddress(string IP, int port)
    {
        m_IPaddress = IP;
        m_port = port;
    }
    public override void Close()
    {
        isConnect = false;
        if (m_Socket != null)
        {
            m_Socket.Close(0);
            m_Socket = null;
        }
        if (m_connThread != null)
        {
            m_connThread.Join();
            m_connThread.Abort();
        }
        m_connThread = null;
    }

    public override void Connect()
    {
        Close();

        m_connThread = null;
        m_connThread = new Thread(new ThreadStart(requestConnect));
        m_connThread.Start();
    }

    //请求数据服务连接线程
    void requestConnect()
    {
        try
        {
            m_ConnectStatusCallback(NetworkState.Connecting);

            m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ip = IPAddress.Parse(m_IPaddress);
            
            IPEndPoint ipe = new IPEndPoint(ip, m_port);
            //mSocket.
            m_Socket.Connect(ipe);
            isConnect = true;
            StartReceive();

            m_ConnectStatusCallback(NetworkState.Connected);
        }
        catch (Exception e)
        {
            Debug.LogError("IP :" + m_IPaddress + " Port: " + m_port+" :"+e.ToString());
            isConnect = false;
            m_ConnectStatusCallback(NetworkState.FaildToConnect);
        }

    }

    void StartReceive()
    {
        m_Socket.BeginReceive(m_readData, 0, m_readData.Length, SocketFlags.None, m_acb, m_Socket);
    }
    void EndReceive(IAsyncResult iar) //接收数据
    {
        Socket remote = (Socket)iar.AsyncState;
        int recv = remote.EndReceive(iar);
        if (recv > 0)
        {
            SpiltMessage(m_readData, recv);
        }

        StartReceive();
    }

    //发包
    public void Send(byte[] sendbytes)
    {
        try
        {
            m_Socket.Send(sendbytes);
        }
        catch (Exception e)
        {
            Debug.LogError("Send Message Error : " + e.Message);
        }
    }


    public override void SendMessage(string MessageType,Dictionary<string, object> data)
    {
        ByteArray msg = HeapObjectPoolTool<ByteArray>.GetHeapObject();
        msg.clear();

        List<byte> message = GetSendByte(MessageType, data);

        int len = 3 + message.Count;
        int method = GetMethodIndex(MessageType);

        msg.WriteShort(len);
        msg.WriteByte((byte)(method/100));
        msg.WriteShort(method);

        if (message != null)
            msg.bytes.AddRange(message);
        else
            msg.WriteInt(0);

        Send(msg.Buffer);
    }

    #region 缓冲区

    byte[] m_messageBuffer;
    int m_head = 0;
    int m_total = 0;

    void SpiltMessage(byte[] bytes,int length)
    {
        WriteBytes(bytes, length);

        int i = 0;

        while (GetBufferLength() != 0 && ReadLength() <= GetBufferLength())
        {
            ReceiveDataLoad(ReadByte(ReadLength()));

            if (i>100)
            {
                break;
            }
        }
    }

    void WriteBytes(byte[] bytes,int length)
    {
        for (int i = 0; i < length; i++)
        {
            int pos = m_total + i;

            if (pos >= m_messageBuffer.Length)
            {
                pos -= m_messageBuffer.Length;
            }

            m_messageBuffer[pos] = bytes[i];
        }

        m_total += length;

        if (m_total >= m_messageBuffer.Length)
        {
            m_total -= m_messageBuffer.Length;
        }
    }

    int ReadLength()
    {
        int result = (int)m_messageBuffer[m_head] << 8;

        int nextPos = m_head + 1;

        if (nextPos >= m_messageBuffer.Length)
        {
            nextPos = 0;
        }

        result += (int)m_messageBuffer[nextPos];
        return result + 2;
    }

    int GetBufferLength()
    {
        if(m_total >= m_head)
        {
            return m_total - m_head;
        }
        else
        {
            return m_total + (m_messageBuffer.Length - m_head);
        }
    }

    byte[] ReadByte(int length)
    {
        byte[] bytes = new byte[length];

        if (m_head + length < m_messageBuffer.Length)
        {
            Array.Copy(m_messageBuffer, m_head, bytes, 0, length);
            m_head += length;
        }
        else
        {
            int cutLength = m_messageBuffer.Length - m_head;

            Array.Copy(m_messageBuffer, m_head, bytes, 0, cutLength);
            Array.Copy(m_messageBuffer, 0, bytes, cutLength, length - cutLength);

            m_head = length - cutLength;
        }
        return bytes;
    }

    #endregion


    //解包
    private void ReceiveDataLoad(byte[] bytes)
    {
        try
        {
            //Debug.Log("ReceiveDataLoad : " + BitConverter.ToString(bytes));

            ByteArray ba = HeapObjectPoolTool<ByteArray>.GetHeapObject();

            //用于做数据处理,加解密,或者压缩于解压缩    
            ba.clear();
            ba.Add(bytes);

            NetWorkMessage msg = Analysis(ba);
            m_messageCallBack(msg);
        }
        catch(Exception e)
        {
            Debug.LogError(e.ToString());
        }
    }

    private void ConnectCallback(IAsyncResult asyncConnect)
    {

    }

    private void SendCallback(IAsyncResult asyncSend)
    {
    }

    #region 读取protocol信息

    void ReadProtocolInfo()
    {
        m_protocolInfo = new Dictionary<string, List<Dictionary<string, object>>>();
        string content = ResourceManager.ReadTextFile(m_ProtocolFileName);
        AnalysisProtocolStatus currentStatus = AnalysisProtocolStatus.None;
        List<Dictionary<string, object>> msgInfo = new List<Dictionary<string, object>>();
        Regex rgx = new Regex(@"^message\s(\w+)");

        string[] lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            string currentLine = lines[i];

            if (currentStatus == AnalysisProtocolStatus.None)
            {
                if (currentLine.Contains("message"))
                {
                    
                    string msgName = rgx.Match(currentLine).Groups[1].Value;

                    //Debug.Log("message :->" + msgName + "<-");

                    msgInfo = new List<Dictionary<string, object>>();

                    if (m_protocolInfo.ContainsKey(msgName))
                    {
                        Debug.LogError("protocol 有重复的Key! :" + msgName);
                    }
                    else
                    {
                        m_protocolInfo.Add(msgName, msgInfo);
                    }

                    
                    currentStatus = AnalysisProtocolStatus.Message;
                }
            }
            else
            {
                if (currentLine.Contains("}"))
                {
                    currentStatus = AnalysisProtocolStatus.None;
                    msgInfo = null;
                }
                else
                {
                    if (currentLine.Contains("required"))
                    {
                        Dictionary<string, object> currentFeidInfo = new Dictionary<string, object>();

                        currentFeidInfo.Add("spl", RT_equired);

                        AddName(currentLine, currentFeidInfo);
                        AddType(currentLine, currentFeidInfo);

                        msgInfo.Add(currentFeidInfo);
                    }
                    else if (currentLine.Contains("repeated"))
                    {
                        Dictionary<string, object> currentFeidInfo = new Dictionary<string, object>();

                        currentFeidInfo.Add("spl", RT_repeated);

                        AddName(currentLine, currentFeidInfo);
                        AddType(currentLine, currentFeidInfo);

                        msgInfo.Add(currentFeidInfo);
                    }
                }
            }
        }
    }

    Regex m_TypeRgx = new Regex(@"^\s+\w+\s+(\w+)\s+\w+");

    void AddType(string currentLine, Dictionary<string, object> currentFeidInfo)
    {
        if (currentLine.Contains("int32"))
        {
            currentFeidInfo.Add("type", TYPE_int32);
        }
        else if (currentLine.Contains("string"))
        {
            currentFeidInfo.Add("type", TYPE_string);
        }
        else if (currentLine.Contains("double"))
        {
            currentFeidInfo.Add("type", TYPE_double);
        }
        else if (currentLine.Contains("bool"))
        {
            currentFeidInfo.Add("type", TYPE_bool);
        }
        else
        {
            currentFeidInfo.Add("type", TYPE_custom);
            currentFeidInfo.Add("vp", m_TypeRgx.Match(currentLine).Groups[1].Value);

        }
    }

    Regex m_NameRgx = new Regex(@"^\s+\w+\s+\w+\s+(\w+)");

    void AddName(string currentLine, Dictionary<string, object> currentFeidInfo)
    {
        currentFeidInfo.Add("name", m_NameRgx.Match(currentLine).Groups[1].Value);
    }

    enum AnalysisProtocolStatus
    {
        None,
        Message
    }

    #endregion

    #region 读取消息号映射

    void ReadMethodNameInfo()
    {
        m_methodNameInfo = new Dictionary<int, string>();
        m_methodIndexInfo = new Dictionary<string, int>();

        string content = ResourceManager.ReadTextFile(m_methodNameInfoFileName);
        Regex rgx = new Regex(@"^(\d+),(\w+)");

        string[] lines = content.Split('\n');

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(","))
            {
                var res = rgx.Match(lines[i]);

                string index = res.Groups[1].Value;
                string indexName = res.Groups[2].Value;

                m_methodNameInfo.Add(int.Parse(index), indexName);
                m_methodIndexInfo.Add(indexName, int.Parse(index));
            }
        }
    }

    int GetMethodIndex(string messageType)
    {
        try
        {
            return m_methodIndexInfo[messageType];
        }
        catch
        {
             throw new Exception("GetMethodIndex ERROR! NOT Find " + messageType);
        }
    }

    #endregion

    #region 解包

    NetWorkMessage  Analysis(ByteArray bytes)
    {
        NetWorkMessage msg = GetMessageByPool();
        //Debug.Log("ReceiveDataLoad : " + BitConverter.ToString(bytes));
        bytes.ReadShort(); //消息长度
        bytes.ReadByte();  //模块名

        int methodIndex = bytes.ReadShort(); //方法名

        msg.m_MessageType = m_methodNameInfo[methodIndex];
        int re_len = bytes.Length - 5;
        msg.m_data = AnalysisData(msg.m_MessageType, bytes.ReadBytes(re_len));


        return msg;
    }

    #region 解析数据

    Dictionary<string, object> AnalysisData(string MessageType, byte[] bytes)
    {
        string fieldName = "";
        string customType = "";
        int fieldType = 0;
        int repeatType = 0;
        try
        {
            Dictionary<string, object> data = HeapObjectPool.GetSODict();
            ByteArray ba = HeapObjectPoolTool<ByteArray>.GetHeapObject();

            ba.clear();
            ba.Add(bytes);

            string messageTypeTemp = "m_" + MessageType + "_c";
            if (!m_protocolInfo.ContainsKey(messageTypeTemp))
            {
                throw new Exception("ProtocolInfo NOT Exist ->" + messageTypeTemp + "<-");
            }

            List<Dictionary<string, object>> tableInfo = m_protocolInfo["m_" + MessageType + "_c"];

            for (int i = 0; i < tableInfo.Count; i++)
            {
                fieldType = (int)tableInfo[i]["type"];
                repeatType = (int)tableInfo[i]["spl"];
                fieldName = (string)tableInfo[i]["name"];

                if (fieldType == TYPE_string)
                {
                    if (repeatType == RT_repeated)
                    {
                        data[fieldName] = ReadStringList(ba);
                    }
                    else
                    {
                        data[fieldName] = ReadString(ba);
                    }
                }
                else if (fieldType == TYPE_bool)
                {
                    if (repeatType == RT_repeated)
                    {
                        data[fieldName] = ReadBoolList(ba);
                    }
                    else
                    {
                        data[fieldName] = ReadBool(ba);
                    }
                }
                else if (fieldType == TYPE_double)
                {
                    if (repeatType == RT_repeated)
                    {
                        data[fieldName] = ReadDoubleList(ba);
                    }
                    else
                    {
                        data[fieldName] = ReadDouble(ba);
                    }
                }
                else if (fieldType == TYPE_int32)
                {
                    if (repeatType == RT_repeated)
                    {
                        data[fieldName] = ReadIntList(ba);
                    }
                    else
                    {
                        data[fieldName] = ReadInt(ba);
                    }
                }
                else
                {
                    customType = (string)tableInfo[i]["vp"];

                    if (repeatType == RT_repeated)
                    {
                        data[fieldName] = ReadDictionaryList(customType, ba);
                    }
                    else
                    {
                        data[fieldName] = ReadDictionary(customType, ba);
                    }
                }
            }

            return data;
        }
        catch(Exception e)
        {
            throw new Exception(@"AnalysisData Excepiton Data is ->" + MessageType
                        + "<-\nFieldName:->" + fieldName
                        + "<-\nFieldType:->" + GetFieldType(fieldType)
                        + "<-\nRepeatType:->" + GetRepeatType(repeatType)
                        + "<-\nCustomType:->" + customType
                        + "<-\n" + e.ToString());
        }
    }

    private string ReadString(ByteArray ba)
    {
        uint len = (uint)ba.ReadShort();
        return ba.ReadUTFBytes(len);
    }
    private List<string> ReadStringList(ByteArray ba)
    {
        List<string> tbl = HeapObjectPoolTool<List<string>>.GetHeapObject();
        tbl.Clear();

        int len1 = ba.ReadShort();
        ba.ReadInt();

        for (int i = 0; i < len1; i++)
        {
            tbl.Add(ReadString(ba));
        }
        return tbl;
    }
    private bool ReadBool(ByteArray ba)
    {
        return ba.ReadBoolean();
    }
    private List<bool> ReadBoolList(ByteArray ba)
    {
        List<bool> tbl = HeapObjectPoolTool<List<bool>>.GetHeapObject();
        tbl.Clear();

        int len1 = ba.ReadShort();
        ba.ReadInt();

        for (int i = 0; i < len1; i++)
        {
            tbl.Add(ReadBool(ba));
        }
        return tbl;
    }
    private int ReadInt(ByteArray ba)
    {
        return ba.ReadInt();
    }
    private List<int> ReadIntList(ByteArray ba)
    {
        List<int> tbl = HeapObjectPoolTool<List<int>>.GetHeapObject();
        tbl.Clear();

        int len1 = ba.ReadShort();
        //Debug.Log("len1    ---- "+ len1);
        ba.ReadInt();

        for (int i = 0; i < len1; i++)
        {
            int tem_o_read_int = ReadInt(ba);
            tbl.Add(tem_o_read_int);
        }

        return tbl;
    }

    private double ReadDouble(ByteArray ba)
    {
        double tem_double = ba.ReadDouble();
        return Math.Floor(tem_double * 1000) / 1000;
    }
    private List<double> ReadDoubleList(ByteArray ba)
    {
        List<double> tbl = HeapObjectPoolTool<List<double>>.GetHeapObject();
        tbl.Clear();

        int len1 = ba.ReadShort();
        ba.ReadInt();

        for (int i = 0; i < len1; i++)
        {
            tbl.Add(ReadDouble(ba));
        }
        return tbl;
    }

    private Dictionary<string, object> ReadDictionary(string dictName, ByteArray ba)
    {
        int fieldType = 0;
        int repeatType = 0;
        string fieldName = null;
        string customType = null;

        try
        {
            int st_len = ba.ReadInt();

            Dictionary<string, object> tbl = HeapObjectPool.GetSODict();

            if (st_len == 0)
            {
                return tbl;
            }

            List<Dictionary<string, object>> tableInfo = m_protocolInfo[dictName];

            for (int i = 0; i < tableInfo.Count; i++)
            {
                fieldType = (int)tableInfo[i]["type"];
                repeatType = (int)tableInfo[i]["spl"];
                fieldName = (string)tableInfo[i]["name"];

                if (fieldType == TYPE_string)
                {
                    if (repeatType == RT_repeated)
                    {
                        tbl[fieldName] = ReadStringList(ba);
                    }
                    else
                    {
                        tbl[fieldName] = ReadString(ba);
                    }
                }
                else if (fieldType == TYPE_bool)
                {
                    if (repeatType == RT_repeated)
                    {
                        tbl[fieldName] = ReadBoolList(ba);
                    }
                    else
                    {
                        tbl[fieldName] = ReadBool(ba);
                    }
                }
                else if (fieldType == TYPE_double)
                {
                    if (repeatType == RT_repeated)
                    {
                        tbl[fieldName] = ReadDoubleList(ba);
                    }
                    else
                    {
                        tbl[fieldName] = ReadDouble(ba);
                    }
                }
                else if (fieldType == TYPE_int32)
                {
                    if (repeatType == RT_repeated)
                    {
                        tbl[fieldName] = ReadIntList(ba);



                    }
                    else
                    {
                        tbl[fieldName] = ReadInt(ba);

                    }
                }
                else
                {
                    customType = (string)tableInfo[i]["vp"];

                    if (repeatType == RT_repeated)
                    {
                        tbl[fieldName] = ReadDictionaryList(customType, ba);
                    }
                    else
                    {
                        tbl[fieldName] = ReadDictionary(customType, ba);
                    }
                }
            }
            return tbl;

        }
        catch(Exception e)
        {
            throw new Exception(@"ReadDictionary Excepiton DictName is ->" + dictName
                        + "<-\nFieldName:->" + fieldName
                        + "<-\nFieldType:->" + GetFieldType(fieldType)
                        + "<-\nRepeatType:->" + GetRepeatType(repeatType)
                        + "<-\nCustomType:->" + customType
                        + "<-\n" + e.ToString());
        }
    }

    private List<Dictionary<string, object>> ReadDictionaryList(string str, ByteArray ba)
    {
        List<Dictionary<string, object>> stbl = HeapObjectPoolTool<List<Dictionary<string, object>>>.GetHeapObject();

        stbl.Clear();
        int len1 = ba.ReadShort();
        ba.ReadInt();

        for (int i = 0; i < len1; i++)
        {
            stbl.Add(ReadDictionary(str, ba));
        }
        return stbl;
    }

    #endregion

    #endregion

    #region 发包

    List<byte> GetSendByte(string messageType, Dictionary<string, object> data)
    {
        try
        {
            string messageTypeTemp = "m_" + messageType + "_s";
            if (!m_protocolInfo.ContainsKey(messageTypeTemp))
            {
                throw new Exception("ProtocolInfo NOT Exist ->" + messageTypeTemp + "<-");
            }

            return GetCustomTypeByte(messageTypeTemp, data);
        }
        catch (Exception e)
        {
            throw new Exception(@"ProtocolService GetSendByte Excepiton messageType is ->" + messageType
                + "<-\n" + e.ToString());
        }
    }

    int GetStringListLength(List<object> list)
    {
        int len = 0;
        for (int i = 0; i < list.Count; i++)
        {
            byte[] bs = Encoding.UTF8.GetBytes((string)list[i]);
            len = len + bs.Length;

        }
        return len;
    }

    List<List<byte>> m_arrayCatch = new List<List<byte>>();
    int GetCustomListLength(string customType,List<object> list)
    {
        m_arrayCatch.Clear();
        int len = 0;
        for (int i = 0; i < list.Count; i++)
        {
            List<byte> bs = GetCustomTypeByte(customType, (Dictionary<string, object>)list[i]);
            m_arrayCatch.Add(bs);
            len = len + bs.Count + 4;
        }
        return len;
    }

    private List<byte> GetCustomTypeByte(string customType, Dictionary<string, object> data)
    {
        string fieldName = null;
        int fieldType = 0;
        int repeatType = 0;

        try
        {
            ByteArray Bytes = HeapObjectPoolTool<ByteArray>.GetHeapObject();
            Bytes.clear();

            if (!m_protocolInfo.ContainsKey(customType))
            {
                throw new Exception("ProtocolInfo NOT Exist ->" + customType + "<-");
            }

            List<Dictionary<string, object>> tableInfo = m_protocolInfo[customType];

            for (int i = 0; i < tableInfo.Count; i++)
            {
                Dictionary<string, object> currentField = tableInfo[i];
                fieldType = (int)currentField["type"];
                fieldName = (string)currentField["name"];
                repeatType = (int)currentField["spl"];

                if (fieldType == TYPE_string)
                {
                    if (data.ContainsKey(fieldName))
                    {
                        if (repeatType == RT_equired)
                        {
                            Bytes.WriteString((string)data[fieldName]);
                        }
                        else
                        {
                            List<object> list = (List<object>)data[fieldName];

                            Bytes.WriteShort(list.Count);
                            Bytes.WriteInt(GetStringListLength(list));
                            for (int i2 = 0; i2 < list.Count; i2++)
                            {
                                Bytes.WriteString((string)list[i2]);
                            }
                        }
                    }
                    else
                    {
                        Bytes.WriteShort(0);
                    }
                }
                else if (fieldType == TYPE_bool)
                {
                    if (data.ContainsKey(fieldName))
                    {
                        if (repeatType == RT_equired)
                        {
                            Bytes.WriteBoolean((bool)data[fieldName]);
                        }
                        else
                        {
                            List<object> tb = (List<object>)data[fieldName];
                            Bytes.WriteShort(tb.Count);
                            Bytes.WriteInt(tb.Count);
                            for (int i2 = 0; i2 < tb.Count; i2++)
                            {
                                Bytes.WriteBoolean((bool)tb[i2]);
                            }
                        }
                    }
                }
                else if (fieldType == TYPE_double)
                {
                    if (data.ContainsKey(fieldName))
                    {
                        if (repeatType == RT_equired)
                        {
                            Bytes.WriteDouble((float)data[fieldName]);
                        }
                        else
                        {
                            List<object> tb = (List<object>)data[fieldName];
                            Bytes.WriteShort(tb.Count);
                            Bytes.WriteInt(tb.Count * 8);
                            for (int j = 0; j < tb.Count; j++)
                            {
                                Bytes.WriteDouble((float)tb[j]);
                            }
                        }
                    }
                }
                else if (fieldType == TYPE_int32)
                {
                    if (data.ContainsKey(fieldName))
                    {
                        if (repeatType == RT_equired)
                        {
                            Bytes.WriteInt(int.Parse(data[fieldName].ToString()));
                        }
                        else
                        {
                            List<object> tb = (List<object>)data[fieldName];
                            Bytes.WriteShort(tb.Count);
                            Bytes.WriteInt(tb.Count * 4);
                            for (int i2 = 0; i2 < tb.Count; i2++)
                            {
                                Bytes.WriteInt(int.Parse(tb[i2].ToString()));
                            }
                        }
                    }
                }
                else
                {
                    if (data.ContainsKey(fieldName))
                    {
                        if (repeatType == RT_equired)
                        {
                            customType = (string)currentField["vp"];
                            Bytes.bytes.AddRange(GetSendByte(customType, (Dictionary<string, object>)data[fieldName]));
                        }
                        else
                        {
                            List<object> tb = (List<object>)data[fieldName];

                            Bytes.WriteShort(tb.Count);
                            //这里会修改m_arrayCatch的值，下面就可以直接使用
                            Bytes.WriteInt(GetCustomListLength(customType, tb));

                            for (int j = 0; j < m_arrayCatch.Count; j++)
                            {
                                List<byte> tempb = m_arrayCatch[j];
                                Bytes.WriteInt(tempb.Count);
                                Bytes.bytes.AddRange(tempb);
                            }
                        }
                    }
                }
            }

            return Bytes.bytes;
        }
        catch(Exception e)
        {
            throw new Exception(@"GetCustomTypeByte Excepiton CustomType is ->" + customType
               + "<-\nFieldName:->" + fieldName
               + "<-\nFieldType:->" + GetFieldType(fieldType)
               + "<-\nRepeatType:->" + GetRepeatType(repeatType)
               + "<-\nCustomType:->" + customType
               + "<-\n" + e.ToString());
        }
    }

    string GetFieldType(int fieldType)
    {
        switch(fieldType)
        {
            case TYPE_string:return"TYPE_string";
            case TYPE_int32:return"TYPE_int32";
            case TYPE_double:return"TYPE_double";
            case TYPE_bool:return"TYPE_bool";
            case TYPE_custom:return"TYPE_custom";
            default: return "Error";
        }
    }

    string GetRepeatType(int repeatType)
    {
        switch (repeatType)
        {
            case RT_repeated: return "RT_repeated";
            case RT_equired: return "RT_equired";
            default: return "Error";
        }
    }

    #endregion
}
