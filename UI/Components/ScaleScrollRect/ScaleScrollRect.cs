using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
/*
 * 整体偏移RectTransfrom以移动Item
 */
public class ScaleScrollRect : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public bool m_isSetScale = true;
    public float m_maxScale = 2; //放大多少倍
    public float m_minScale = 1;

    public Transform m_content;

    public float m_itemSpace = 45 + 226;         //每个item的间隔   
    public float m_smooth = 0.1f;

    Vector2 m_mousePosition = new Vector2();
    bool m_isMove = false;

    int m_aimIndex = 0;
    int m_currentHighLightIndex = 0; //当前高亮显示的对象索引号

    float m_speed = 0;

    RectTransform m_rect;

    public List<GameObject> m_itemList = new List<GameObject>();

    #region 外部调用

    //public void Start()
    //{
    //    Init();
    //}

    public void Init()
    {
        m_rect = GetComponent<RectTransform>();


        //rtf.sizeDelta = new Vector2(itemSpace * itemList.Count * 3, panelHigh);
    }

    public void SetData(string itemName ,List<Dictionary<string,object>> dataList)
    {
        for (int i = 0; i < dataList.Count; i++)
        {
            GameObject item = GameObjectManager.CreateGameObjectByPool(itemName,m_rect.gameObject);
            m_itemList.Add(item);

            //设置数据
            ScaleScrollItemBase ssi = item.GetComponent<ScaleScrollItemBase>();
            ssi.Init(i);
            ssi.SetData(dataList[i]);

            //调整位置
            item.transform.localPosition = new Vector3(i * m_itemSpace, 0, 0);
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < m_itemList.Count; i++)
        {
            ScaleScrollItemBase ssi = m_itemList[i].GetComponent<ScaleScrollItemBase>();
            ssi.Dispose();

            GameObjectManager.DestroyGameObjectByPool(m_itemList[i]);
        }

        m_itemList.Clear();
    }

    #endregion

    #region 重载方法

    public virtual void ShowLogic()
    {
        if (m_isSetScale)
        {
            SetScale();
        }
    }

    #endregion

    #region Update

    void Update()
    {
        if (m_isMove)
        {
            float aimX = m_aimIndex * m_itemSpace * -1;
            float X = Mathf.SmoothDamp(m_rect.anchoredPosition3D.x, aimX, ref m_speed, m_smooth);
            m_rect.anchoredPosition3D = new Vector3(X, m_rect.anchoredPosition3D.y, 0);

            ShowLogic();
        }
    }

    #endregion

    #region 功能函数

    public void OnBeginDrag(PointerEventData eventData)
    {
        m_isMove = false;
        m_mousePosition = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - m_mousePosition.x;
        m_mousePosition = eventData.position;
        MovePanel(deltaX);
        ShowLogic();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        m_isMove = true;
        GetAimPosition(); //获取要移动到的目标地点
        m_currentHighLightIndex = m_aimIndex;
    }

    void MovePanel(float deltaX)
    {
        if (m_rect == null) return;

        //Debug.Log(rtf.anchoredPosition3D);

        m_rect.anchoredPosition3D = new Vector3(m_rect.anchoredPosition3D.x + deltaX, m_rect.anchoredPosition3D.y, 0);

        if (m_rect.anchoredPosition3D.x >= 0) //限定拖动范围
        {
            m_rect.anchoredPosition3D = new Vector3(0, m_rect.anchoredPosition3D.y, 0);

            //leftButton.SetActive(false);
        }
        else
        {
            //leftButton.SetActive(true);
        }

        if (m_rect.anchoredPosition3D.x <= (m_itemList.Count) * m_itemSpace * -1)//限定拖动范围
        {
            m_rect.anchoredPosition3D = new Vector3((m_itemList.Count) * m_itemSpace * -1, m_rect.anchoredPosition3D.y, 0);

            //rightButton.SetActive(false);
        }
        else
        {
            //rightButton.SetActive(true);
        }
    }
    void GetAimPosition()
    {
        int index = (int)(m_rect.anchoredPosition3D.x / m_itemSpace) * -1;
        float distance = m_rect.anchoredPosition3D.x % m_itemSpace * -1;

        if (distance > (m_itemSpace / 2))
        {
            m_aimIndex = index + 1;
            if (index + 1 < m_itemList.Count)
            {
                m_aimIndex = index + 1;
            }
            else
            {
                m_aimIndex = index;
            }
        }
        else
        {
            m_aimIndex = index;
        }

        if (m_aimIndex < m_itemList.Count)
        {
            m_itemList[m_aimIndex].transform.SetSiblingIndex(m_itemList.Count); //将目标节点移至最高
        }
        else
        {
            m_aimIndex = m_itemList.Count - 1;
        }
    }

    #endregion

    #region Scale
    void SetScale()
    {
        for (int i = 0; i < m_itemList.Count; i++)
        {
            m_itemList[i].transform.localScale = new Vector3(m_minScale, m_minScale, m_minScale);
        }

        //左侧缩放icon
        int index = (int)(m_rect.localPosition.x / m_itemSpace) * -1;

        float distance = m_rect.localPosition.x % m_itemSpace * -1;

        Vector3 aimScale = new Vector3(m_minScale, m_minScale, m_minScale) + new Vector3(m_maxScale, m_maxScale, m_maxScale) * ((m_itemSpace - distance) / m_itemSpace);

        if (index < m_itemList.Count)
        {
            m_itemList[index].transform.localScale = aimScale;

            //右侧缩放icon
            if (index + 1 < m_itemList.Count) //如果有右侧item
            {
                aimScale = new Vector3(m_minScale, m_minScale, m_minScale) + new Vector3(m_maxScale, m_maxScale, m_maxScale) * ((distance) / m_itemSpace);

                m_itemList[index + 1].transform.localScale = aimScale;
            }
        }
    }
    #endregion
}
