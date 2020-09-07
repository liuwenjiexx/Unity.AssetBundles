using Coroutines;
using System;
using System.Collections;
using System.Collections.Generic;
using COROUTINE = Coroutines.Coroutine;
using System.Linq;

[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]
namespace UnityEngine.Coroutines
{
    [UnityEngine.Scripting.Preserve]
    class UnityCoroutineScheduler : CoroutineScheduler
    {

        private static MonoBehaviour unityScheduler;
        private static MonoBehaviour UnityScheduler
        {
            get
            {

                if (!unityScheduler)
                {
                    GameObject go = new GameObject();
                    go.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideAndDontSave | HideFlags.DontSaveInEditor;
                    GameObject.DontDestroyOnLoad(go);
                    unityScheduler = go.AddComponent<UnityCoroutineSchedulerBehaviour>();
                }
                return unityScheduler;
            }
        }
        [UnityEngine.Scripting.Preserve]
        static CoroutineScheduler CreateDefaultScheduler()
        {
            if (Application.isPlaying)
            {
                return new UnityCoroutineScheduler();
            }
            return null;
        }

        public override double Time
        {
            get { return UnityEngine.Time.time; }
        }

        public override bool Avaliable { get { return Application.isPlaying; } }


        public override COROUTINE QueueRoutine(IEnumerator routine, CancellationToken cancellationToken = null)
        {
            if (routine is COROUTINE)
            {
                routine = CoroutineToRoutine((COROUTINE)routine);
            }
            var coroutine = CreateCoroutine(routine, cancellationToken);
            _QueueRoutine(coroutine, routine);
            return coroutine;
        }
        public override Coroutine<TResult> QueueRoutine<TResult>(IEnumerator routine, CancellationToken cancellationToken = null)
        {
            if (routine is COROUTINE)
            {
                routine = CoroutineToRoutine((COROUTINE)routine);
            }
            var coroutine = CreateCoroutine<TResult>(routine, cancellationToken);
            _QueueRoutine(coroutine, routine);
            return coroutine;
        }

        private void _QueueRoutine(COROUTINE coroutine, IEnumerator routine)
        {
            //if (coroutineData == null)
            {
                if (routine is COROUTINE)
                {
                    routine = CoroutineToRoutine((COROUTINE)routine);
                }
            }
            var cancelToken = GetCancelToken(coroutine);
            if (cancelToken != null)
            {
                if (cancelToken.IsCancellationRequested)
                {
                    SetCoroutineCanceled(coroutine);
                    return;
                }
                //UnityScheduler.StartCoroutine(WaitCancel(coroutine, cancelToken));
            }
            UnityScheduler.StartCoroutine(AsCoroutine(coroutine, routine));
            SetReady(coroutine);

        }

        //private IEnumerator WaitCancel(COROUTINE co, CancellationToken cancellationToken)
        //{
        //    while (!co.IsDone)
        //    {
        //        yield return null;
        //    }
        //}


        private IEnumerator CoroutineToRoutine(COROUTINE co)
        {
            yield return co;
            if (IsRequreReturnValue(co))
                yield return new YieldReturn(co.ReturnValue);
        }
        //public override void Cancel(object context, object id)
        //{
        //    MonoBehaviour unity = unityScheduler;
        //    if (id is UnityEngine.Coroutine)
        //    {
        //        var co = (UnityEngine.Coroutine)id;
        //        unity.StopCoroutine(co);
        //    }
        //    else if (id is string)
        //    {
        //        unity.StopCoroutine((string)id);
        //    }
        //    else if (id is IEnumerator)
        //    {
        //        unity.StopCoroutine((IEnumerator)id);
        //    }
        //}

        //public override void CancelAll(object context)
        //{
        //    MonoBehaviour unity = unityScheduler;
        //    unity.StopAllCoroutines();
        //}



        private IEnumerator AsCoroutine(COROUTINE coroutine, IEnumerator routine)
        {
            object yieldReturn;
            bool hasNext;
            Exception exception = null;
            COROUTINE waitForCoroutine = null;
            CancellationToken cancellationToken = GetCancelToken(coroutine);
            while (true)
            {
                if (!coroutine.IsDone)
                {
                    waitForCoroutine = GetWaitForCoroutine(coroutine);
                    if (waitForCoroutine != null)
                    {
                        //wait done
                        while (!waitForCoroutine.IsDone && !coroutine.IsDone)
                        {
                            if (cancellationToken != null && cancellationToken.IsCancellationRequested)
                            {
                                SetCoroutineCanceled(coroutine);
                                break;
                            }
                            yield return null;
                        }
                        SetWaitForCoroutine(coroutine, null);

                        var ex = waitForCoroutine.Exception;
                        if (!coroutine.IsDone)
                        {
                            if (ex != null)
                            {
                                ICatchable catchable = waitForCoroutine as ICatchable;
                                if (catchable != null)
                                {
                                    try
                                    {
                                        catchable.HandleException(ex);
                                    }
                                    catch (Exception ex2)
                                    {
                                        var newEx = new CoroutineAggregateException(ex2);
                                        newEx = newEx.Flatten();
                                        SetCoroutineException(coroutine, newEx, true);
                                        break;
                                    }
                                }
                                else
                                {
                                    SetCoroutineException(coroutine, ex, false);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        hasNext = false;


                        if (cancellationToken != null && cancellationToken.IsCancellationRequested)
                        {
                            SetCoroutineCanceled(coroutine);
                            break;
                        }

                        lock (lockObj)
                        {

                            PushCurrent();

                            try
                            {

                                hasNext = routine.MoveNext();
                                if (!hasNext)
                                {
                                    CheckReturnValue(coroutine);
                                }
                            }
                            catch (Exception ex)
                            {
                                exception = ex;
                            }
                            finally
                            {
                                PopCurrent();
                            }
                        }

                        if (exception != null)
                        {
                            var aggEx = new CoroutineAggregateException(exception);
                            aggEx = aggEx.Flatten();
                            SetCoroutineException(coroutine, aggEx, true);
                            break;
                        }

                        if (!hasNext)
                            break;

                        yieldReturn = routine.Current;

                        //if (yieldReturn != null)
                        //{
                        //    IEnumerator convertRoutine;
                        //    if (ConvertToRoutine(yieldReturn, out convertRoutine))
                        //        yieldReturn = convertRoutine;
                        //}

                        if (yieldReturn != null)
                        {
                            if (yieldReturn is IYield)
                            {
                                if (yieldReturn is COROUTINE)
                                {
                                    waitForCoroutine = (COROUTINE)yieldReturn;
                                    SetWaitForCoroutine(coroutine, waitForCoroutine);

                                }
                                else if (yieldReturn is WaitForMilliseconds)
                                {
                                    float atTime = (float)(Time + ((WaitForMilliseconds)yieldReturn).Milliseconds * 0.001f);
                                    while (Time < atTime)
                                    {
                                        if (cancellationToken != null && cancellationToken.IsCancellationRequested)
                                        {
                                            SetCoroutineCanceled(coroutine);
                                            break;
                                        }
                                        yield return null;
                                    }
                                }
                                else if (yieldReturn is WaitForFrame)
                                {
                                    int frameCount = ((WaitForFrame)yieldReturn).FrameCount;
                                    if (frameCount <= 0)
                                        yield return null;
                                    else
                                        for (int i = 0; i < frameCount; i++)
                                        {
                                            if (cancellationToken != null && cancellationToken.IsCancellationRequested)
                                            {
                                                SetCoroutineCanceled(coroutine);
                                                break;
                                            }
                                            yield return null;
                                        }
                                }
                                else if (yieldReturn is YieldReturn)
                                {
                                    YieldReturn ret = (YieldReturn)yieldReturn;
                                    SetCoroutineReturnValue(coroutine, ret);
                                }
                                else if (yieldReturn is CustomYield)
                                {
                                    CustomYield customYield = (CustomYield)yieldReturn;
                                    SetWaitForCoroutine(coroutine, QueueRoutine(customYield));
                                }
                                else
                                {
                                    throw new CoroutineUnknownYieldTypeException(yieldReturn.GetType());
                                }
                            }
                            else if (yieldReturn is IEnumerable)
                            {
                                SetWaitForCoroutine(coroutine, QueueRoutine(((IEnumerable)yieldReturn).GetEnumerator()));
                            }
                            else if (yieldReturn is IEnumerator)
                            {
                                SetWaitForCoroutine(coroutine, QueueRoutine((IEnumerator)yieldReturn));
                            }
                            else
                            {
                                IEnumerator tmp;
                                if (ConvertToRoutine(yieldReturn, out tmp))
                                {
                                    SetWaitForCoroutine(coroutine, QueueRoutine(tmp));
                                }
                                else
                                {
                                    // ignore handle
                                    yield return yieldReturn;
                                }
                            }
                        }
                        else
                        {
                            yield return null;
                        }

                    }
                }


                if (coroutine.IsDone)
                    break;
            }

            if (!coroutine.IsDone)
            {
                SetCoroutineDone(coroutine);
            }
            else
            {
                if (!coroutine.IsRanToCompletion)
                {
                    exception = coroutine.Exception;
                    if (exception != null && !IsCoroutineHandleException(coroutine))
                    {
                        if (!IsReady(coroutine))
                            yield return null;
                        if (!IsCoroutineHandleException(coroutine))
                            throw exception;
                    }
                }
            }
        }



        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (unityScheduler)
                {
                    GameObject.Destroy(unityScheduler.gameObject);
                    unityScheduler = null;
                }
            }
        }

    }

}