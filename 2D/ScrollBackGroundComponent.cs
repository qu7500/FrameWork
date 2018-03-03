using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace FrameWork.Game2D
{
    /// <summary>
    /// 用于循环滚动Sprite
    /// </summary>
    public class ScrollBackGroundComponent : PoolObject
    {
        public List<ScrollBackGroundInfo> m_scrollInfo;
        public Vector2 m_currentPos;
        public Rect bound;     //总边界
        public Rect ViewBound; //视界

        Camera m_camera;
        private Vector3 m_pos = Vector3.zero;

        public Vector3 Pos
        {
            get { return m_pos; }
            set { 
                m_pos = value;
                m_pos.x = Mathf.Clamp(m_pos.x, bound.xMin, bound.xMax);
                m_pos.y = Mathf.Clamp(m_pos.y, bound.yMin, bound.yMax);
            }
        }

        [Tooltip("支持鼠标拖动")]
        public bool drag = false;


        public override void OnFetch()
        {
            base.OnFetch();

            for (int i = 0; i < m_scrollInfo.Count; i++)
            {
                m_scrollInfo[i].Init(this,i);
            }
        }

        //public void SetPos(Vector3 pos)
        //{
        //    m_pos = pos;

        //    m_pos.x = Mathf.Clamp(m_pos.x, bound.xMin, bound.xMax);
        //    m_pos.y = Mathf.Clamp(m_pos.y, bound.yMin, bound.yMax);
        //}

        public void SetCamera(Camera camera)
        {
            m_camera = camera;
        }

        private void Update()
        {
            if(m_camera != null)
            {
                Pos = m_camera.transform.position;
            }

            for (int i = 0; i < m_scrollInfo.Count; i++)
            {
                m_scrollInfo[i].SetPos(Pos);
            }
        }
    }

    [System.Serializable]
    public class ScrollBackGroundInfo
    {
        public Vector2 m_offset = Vector2.zero;
        public List<string> m_scrollList;
        //public bool random = false;
        public float scrollSpeed;

        ScrollBackGroundComponent m_root;
        List<Vector2> sizeList = new List<Vector2>(); //保存图片大小
        List<SpriteRenderer> m_rendererList = new List<SpriteRenderer>(); //正在显示的资源

        GameObject m_node;

        public void Init(ScrollBackGroundComponent root,int index)
        {
            m_node = new GameObject("nodel_" + index);
            m_node.transform.SetParent(root.transform);
            m_node.transform.localPosition = Vector3.zero;

            m_root = root;

            Vector3 pos = Vector3.zero;
            for (int i = 0; i < m_scrollList.Count; i++)
            {
                GameObject go =  GameObjectManager.CreateGameObjectByPool(m_scrollList[i], m_node);
                SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                m_rendererList.Add(sr);

                if(i == 0)
                {
                    pos.x -= sr.sprite.textureRect.width / sr.sprite.pixelsPerUnit/2;
                }

                pos.x += sr.sprite.textureRect.width  / sr.sprite.pixelsPerUnit;
                pos.y = sr.sprite.textureRect.height  / sr.sprite.pixelsPerUnit/2;

                go.transform.localPosition = pos;
            }
        }

        public void SetPos(Vector3 pos)
        {
            //清空已有资源
            for (int i = 0; i < m_rendererList.Count; i++)
            {
                GameObjectManager.DestroyGameObjectByPool(m_rendererList[i].gameObject);
            }
            m_rendererList.Clear();

            //计算出应该是哪几个资源
            //计算出对应的位置
            //加载并摆放

            m_offset = pos * scrollSpeed;
            //限制区域
            int index = 0;
            bool isBreak = false;
            bool isShow = false;
            while (!isBreak)
            {
                GroundInfo info = GetGroundInfo(index);
                if (m_root.ViewBound.Overlaps(info.rect))
                {
                    isShow = true;
                    //加载对应资源并摆放
                    CreateSprite(info);
                }
                else
                {
                    //已经显示过，后面的不在显示区域中终止循环
                    if(isShow)
                    {
                        isBreak = true;
                    }
                }
                index++;
            }
        }

        GroundInfo GetGroundInfo(int index)
        {
            return new GroundInfo();
        }

        Vector2 GetGroundSize(int index)
        {
            return Vector2.one;
        }

        Vector2 GetGroundPos(int index)
        {
            return Vector2.one;
        }

        void CreateSprite(GroundInfo info)
        {
            GameObject go = GameObjectManager.CreateGameObjectByPool(info.spriteName, m_node);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            m_rendererList.Add(sr);

            go.transform.localPosition = new Vector3(info.rect.x, info.rect.y, 0);
        }

        struct GroundInfo
        {
            public Rect rect;
            public string spriteName;
        }
    }
}
