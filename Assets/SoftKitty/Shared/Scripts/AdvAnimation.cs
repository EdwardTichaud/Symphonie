using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoftKitty
{
    /// <summary>
    /// Add this compoent along with the legacy 'Animation' component to make it works when timeScale==0
    /// When you call native functions of legacy 'Animation' component, call same functions in this compoent insteaded. For example:  GetComponent<AdvAnimation>().Play();
    /// </summary>
    public class AdvAnimation : MonoBehaviour
    {
         private Animation ani
        {
            get
            {
                if (_ani == null) _ani = GetComponent<Animation>();
                return _ani;
            }
        }
        private Animation _ani;
        private string currentAnimation = "";
        public bool isPlaying
        {
            get
            {
                if (ani != null)
                    return ani.isPlaying;
                else
                    return false;
            }
        }
        public AnimationClip clip
        {
            get
            {
                if (ani != null)
                    return ani.clip;
                else
                    return null;
            }
        }

        public int GetClipCount()
        {
            if (ani != null)
                return ani.GetClipCount();
            else
                return 0;
        }

        public bool Play()
        {
            if (ani == null) return false;
            currentAnimation = ani.clip.name;
            return ani.Play();
        }
        public bool Play(string animation)
        {
            if (ani == null) return false;
            currentAnimation = animation;
            return ani.Play(animation);
        }

        public void Stop()
        {
            if (ani == null) return;
            ani.Stop();
            if (ani.clip != null) currentAnimation = ani.clip.name;
        }

        public bool IsPlaying(string name)
        {
            if (ani == null) return false;
            return ani.IsPlaying(name);
        }

        private void Start()
        {
            if (ani == null) return;
            if (ani.clip!=null)currentAnimation = ani.clip.name;
        }

        private void Update()
        {
            if (Time.timeScale>0F || !ani.isPlaying || string.IsNullOrEmpty(currentAnimation)) return;
            AnimationState currentState = ani[currentAnimation];
            if (currentState.time < currentState.length)
            {
                currentState.time += Time.unscaledDeltaTime;
                ani.Sample();
            }
        }

    }
}
