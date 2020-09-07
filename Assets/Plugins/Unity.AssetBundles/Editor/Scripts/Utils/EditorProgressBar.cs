using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;

namespace UnityEditor
{

    class EditorProgressBar : IDisposable
    {
        private string title;
        private string info;
        private float progress;
        private bool isDisposed;
        private static Stack<EditorProgressBar> progressBars = new Stack<EditorProgressBar>();


        public EditorProgressBar(string title)
            : this(title, string.Empty, 0f)
        {

        }
        public EditorProgressBar(string title, string info, float progress)
        {
            this.title = title;
            this.info = info;
            this.progress = progress;
            progressBars.Push(this);
            Show();
        }

        public string Title
        {
            get => title; set
            {
                title = value ?? string.Empty;
                Show();
            }
        }
        public string Info
        {
            get => info; set
            {
                info = value ?? string.Empty;
                Show();
            }
        }
        public float Progress
        {
            get => progress; set
            {
                progress = Mathf.Clamp01(value);
                Show();
            }
        }

        EditorProgressBar Current
        {
            get { return progressBars.Count > 0 ? progressBars.Peek() : null; }
        }
        public void Show()
        {
            if (Current != this)
                return;
            if (isDisposed)
            {
                Clear();
                return;
            }
            EditorUtility.DisplayProgressBar(title, info, progress);
        }

        public void OnProgress(string info, float progress)
        {
            this.info = info;
            this.progress = progress;
            Show();
        }

        void Clear()
        {
            if (Current == this && isDisposed)
            {
                progressBars.Pop();
                if (progressBars.Count > 0)
                    progressBars.Peek().Show();
                else
                {
                    EditorUtility.ClearProgressBar();
                }
            }
        }

        public void Dispose()
        {
            if (!isDisposed)
            {
                isDisposed = true;
                Clear();
            }
        }
    }

}