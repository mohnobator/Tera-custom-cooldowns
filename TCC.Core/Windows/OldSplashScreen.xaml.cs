﻿using System.Net;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FoglioUtils;

namespace TCC.Windows
{
    public partial class OldSplashScreen
    {
        public SplashScreenViewModel VM { get; }

        public OldSplashScreen()
        {
            InitializeComponent();
            VM = new SplashScreenViewModel();
            DataContext = VM;
        }

        public new void CloseWindowSafe()
        {
            Dispatcher.Invoke(() =>
            {
                var an = AnimationFactory.CreateDoubleAnimation(300, 0, easing: true, completed: (s, ev) =>
                {
                    Close();
                    Dispatcher.InvokeShutdown();
                });

                BeginAnimation(OpacityProperty, an);
            });
        }
    }
}