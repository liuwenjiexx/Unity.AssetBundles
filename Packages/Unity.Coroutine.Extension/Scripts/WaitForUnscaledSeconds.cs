using UnityEngine;
using System.Collections;
using Coroutines;

namespace UnityEngine.Coroutines
{

    public class WaitForUnscaledSeconds : IEnumerable
    {
        private float seconds;
        public WaitForUnscaledSeconds(float seconds)
        { 
            this.seconds =  seconds;
        }
 
        public IEnumerator GetEnumerator()
        {
            float time= Time.unscaledTime + seconds;
            while (Time.unscaledTime < time)
                yield return null;
        }
    }

    

}
