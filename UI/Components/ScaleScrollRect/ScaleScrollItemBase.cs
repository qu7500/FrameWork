using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScaleScrollItemBase : UIBase 
{
    public virtual void SetData(Dictionary<string,object> data)
    {

    }

    public virtual void Dispose()
    {

    }

    /// <summary>
    /// 被选中调用
    /// </summary>
    public  virtual void OnSelect()
    {

    }

    /// <summary>
    /// 取消选中调用
    /// </summary>
    public virtual void OnUnSelect()
    {

    }
}
