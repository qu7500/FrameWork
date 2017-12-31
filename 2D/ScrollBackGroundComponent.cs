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
        public Rect bound;

        Camera m_camera;
        public Vector3 m_pos = Vector3.zero;

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

        public void SetPos(Vector3 pos)
        {
            m_pos = pos;

            m_pos.x = Mathf.Clamp(m_pos.x, bound.xMin, bound.xMax);
            m_pos.y = Mathf.Clamp(m_pos.y, bound.yMin, bound.yMax);
        }

        public void SetCamera(Camera camera)
        {
            m_camera = camera;
        }

        private void Update()
        {
            if(m_camera != null)
            {
                SetPos(m_camera.transform.position);
            }

            for (int i = 0; i < m_scrollInfo.Count; i++)
            {
                m_scrollInfo[i].SetPos(m_pos);
            }
        }
    }

    [System.Serializable]
    public class ScrollBackGroundInfo
    {
        public List<string> m_scrollList;
        //public bool random = false;
        public float scrollSpeed;

        ScrollBackGroundComponent m_root;
        List<SpriteRenderer> m_rendererList = new List<SpriteRenderer>();

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

        }
    }
}
