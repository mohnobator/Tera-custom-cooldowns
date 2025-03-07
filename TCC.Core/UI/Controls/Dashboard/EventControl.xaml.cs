﻿using Nostrum.WPF.Factories;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TCC.UI.Controls.Dashboard
{
    public partial class EventControl
    {
        private readonly DoubleAnimation _scaleUp;
        private readonly DoubleAnimation _scaleDown;

        public EventControl()
        {
            _scaleUp = AnimationFactory.CreateDoubleAnimation(800, 1.01);
            _scaleUp.EasingFunction = new ElasticEase();
            _scaleDown = AnimationFactory.CreateDoubleAnimation(150, 1, easing: true);

            InitializeComponent();
        }

        private void UserControl_MouseEnter(object sender, MouseEventArgs e)
        {
            Border.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, _scaleUp);
            Border.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, _scaleUp);
        }

        private void UserControl_MouseLeave(object sender, MouseEventArgs e)
        {
            Border.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, _scaleDown);
            Border.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, _scaleDown);
        }
    }
}
