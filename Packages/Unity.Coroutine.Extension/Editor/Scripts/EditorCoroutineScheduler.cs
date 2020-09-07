using Coroutines;
using Coroutines.Threading;
using UnityEngine;

namespace UnityEditor.Coroutines
{


    class EditorCoroutineScheduler : ThreadCoroutineScheduler
    {

        public EditorCoroutineScheduler()
        {

            AddRoutineConverter(new EditorRoutineConverter());

            UnhandledExceptionCallback += UnhandledException_Callback;

            EditorApplication.update -= EditorApplication_Update;
            EditorApplication.update += EditorApplication_Update;

        }

        static CoroutineScheduler CreateDefaultScheduler()
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                return new EditorCoroutineScheduler();
            }
            return null;
        }

        public override bool Avaliable { get { return Application.isEditor && !Application.isPlaying; } }



        private void EditorApplication_Update()
        {
            if (CoroutineScheduler.Defualt != this)
            {
                Dispose();
                return;
            }
            //float deltaTime = (float)(EditorApplication.timeSinceStartup - Time);
            double time = EditorApplication.timeSinceStartup;

            UpdateFrame(time);
        }

        void UnhandledException_Callback(CoroutineScheduler scheduer, CoroutineAggregateException ex)
        {
            Debug.LogException(ex);
        }


        private bool isDisposed;



        private void Dispose()
        {
            if (isDisposed)
                return;
            lock (lockObj)
            {
                if (!isDisposed)
                {
                    isDisposed = true;
                    EditorApplication.update -= EditorApplication_Update;
                }
            }
        }

        ~EditorCoroutineScheduler()
        {

            Dispose();
        }

    }

}