using Hardcodet.Wpf.TaskbarNotification;

using LUC.Interfaces.Enums;

using System;
using System.Drawing;
using System.Windows.Threading;

namespace LUC.WpfClient
{
    public enum IconEnum
    {
        IconsSource1 = 0, IconsSource2 = 1, IconsSource3 = 2, IconsSource4 = 3, IconsSource5 = 5, IconsSource6 = 6
    }

    public class NotifyIconAnimator
    {
        private readonly DispatcherTimer m_dispatcherTimer;
        private IconEnum m_currentIconState = IconEnum.IconsSource1;
        private NotifyIconAnimationType m_currentDirection = NotifyIconAnimationType.Default;
        private readonly TaskbarIcon m_notifyIcon;

        public NotifyIconAnimator( TaskbarIcon notifyIcon )
        {
            this.m_notifyIcon = notifyIcon;
            m_dispatcherTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds( 0.20 )
            };
        }

        public NotifyIconAnimationType CurrentDirectionSync => m_currentDirection;

        private void SubscribeAndRun( EventHandler eventHandler )
        {
            m_dispatcherTimer.Tick += eventHandler;
            m_dispatcherTimer.Start();
        }

        public void StopIconAnimation()
        {
            m_dispatcherTimer.Stop();
            m_notifyIcon.Icon = new Icon( @"LightSquareIcon32x32.ico" );
        }

        public void RunIconAnimation( NotifyIconAnimationType sourse )
        {
            m_currentIconState = IconEnum.IconsSource1;
            if ( m_currentDirection == NotifyIconAnimationType.Download )
            {
                m_dispatcherTimer.Tick -= AnimateDownload;
            }
            else if ( m_currentDirection == NotifyIconAnimationType.Upload )
            {
                m_dispatcherTimer.Tick -= AnimateUpload;
            }

            m_currentDirection = sourse;

            switch ( m_currentDirection )
            {
                case NotifyIconAnimationType.Default:
                {
                    StopIconAnimation();
                }

                break;
                case NotifyIconAnimationType.Download:
                {
                    SubscribeAndRun( AnimateDownload );
                }

                break;
                case NotifyIconAnimationType.Upload:
                {
                    SubscribeAndRun( AnimateUpload );
                }

                break;
                default:
                {
                    throw new NotImplementedException();
                }
            }
        }

        private void AnimateUpload( Object sender, EventArgs e )
        {
            switch ( m_currentIconState )
            {
                case IconEnum.IconsSource1:
                    m_notifyIcon.Icon = new Icon( @"Icons\Upload1.ico" );
                    m_currentIconState = IconEnum.IconsSource2;
                    break;
                case IconEnum.IconsSource2:
                    m_notifyIcon.Icon = new Icon( @"Icons\Upload2.ico" );
                    m_currentIconState = IconEnum.IconsSource3;
                    break;
                case IconEnum.IconsSource3:
                    m_notifyIcon.Icon = new Icon( @"Icons\Upload3.ico" );
                    m_currentIconState = IconEnum.IconsSource4;
                    break;
                case IconEnum.IconsSource4:
                    m_notifyIcon.Icon = new Icon( @"Icons\Upload4.ico" );
                    m_currentIconState = IconEnum.IconsSource5;
                    break;
                case IconEnum.IconsSource5:
                    m_notifyIcon.Icon = new Icon( @"Icons\Upload5.ico" );
                    m_currentIconState = IconEnum.IconsSource6;
                    break;
                case IconEnum.IconsSource6:
                    m_notifyIcon.Icon = new Icon( @"Icons\Upload6.ico" );
                    m_currentIconState = IconEnum.IconsSource1;
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void AnimateDownload( Object sender, EventArgs e )
        {
            switch ( m_currentIconState )
            {
                case IconEnum.IconsSource1:
                    m_notifyIcon.Icon = new Icon( @"Icons\Download1.ico" );
                    m_currentIconState = IconEnum.IconsSource2;
                    break;
                case IconEnum.IconsSource2:
                    m_notifyIcon.Icon = new Icon( @"Icons\Download2.ico" );
                    m_currentIconState = IconEnum.IconsSource3;
                    break;
                case IconEnum.IconsSource3:
                    m_notifyIcon.Icon = new Icon( @"Icons\Download3.ico" );
                    m_currentIconState = IconEnum.IconsSource4;
                    break;
                case IconEnum.IconsSource4:
                    m_notifyIcon.Icon = new Icon( @"Icons\Download4.ico" );
                    m_currentIconState = IconEnum.IconsSource5;
                    break;
                case IconEnum.IconsSource5:
                    m_notifyIcon.Icon = new Icon( @"Icons\Download5.ico" );
                    m_currentIconState = IconEnum.IconsSource6;
                    break;
                case IconEnum.IconsSource6:
                    m_notifyIcon.Icon = new Icon( @"Icons\Download6.ico" );
                    m_currentIconState = IconEnum.IconsSource1;
                    break;
                default:
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
