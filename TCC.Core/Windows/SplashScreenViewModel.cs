﻿using System;
using System.IO;
using System.Threading;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using TCC.Controls;

namespace TCC.Windows
{
    public class SplashScreenViewModel : TSPropertyChanged
    {
        private int _progress;
        private bool _waiting;
        private string _bottomText = "TCC";

        public event Action ProgressChangedEvent;

        public int Progress
        {
            get => _progress;
            set
            {
                if (_progress == value) return;
                _progress = value;
                ProgressChangedEvent?.Invoke();
            }
        }
        public double ProgressPerc => Progress / 100d;

        public bool Waiting
        {
            get => _waiting;
            private set
            {
                if(_waiting == value) return;
                _waiting = value;
                N();
            }
        }
        public string BottomText
        {
            get => _bottomText;
            set
            {
                if (_bottomText == value) return;
                _bottomText = value;
                N();
            }
        }

        public string Version => App.AppVersion.Replace("TCC ", "").Replace("-e", "");
        public bool Experimental => App.Experimental;
        public bool Toolbox => App.ToolboxMode;

        public bool Answer { get; private set; }

        public ImageSource Image { get; }

        public ICommand OkCommand { get; }
        public ICommand NoCommand { get; }

        public SplashScreenViewModel()
        {
            try
            {
                var path = Path.Combine(App.ResourcesPath, $"images/splash/{App.Random.Next(1, 15)}.jpg");
                var bm = new BitmapImage();
                bm.BeginInit();
                bm.UriSource = new Uri(App.FI ? "pack://application:,,,/resources/images/10kdays.jpg" : path, UriKind.Absolute);
                bm.CacheOption = BitmapCacheOption.OnLoad;
                bm.EndInit();
                Image = bm;
            }
            catch { }

            OkCommand = new RelayCommand(args =>
            {
                Answer = true;
                Waiting = false;
            });
            NoCommand = new RelayCommand(args =>
            {
                Answer = false;
                Waiting = false;
            });
        }

        public bool AskUpdate(string updateText)
        {
            Waiting = true;
            BottomText = updateText;
            while (Waiting)
            {
                Thread.Sleep(100);
            }

            return Answer;
        }
    }
}