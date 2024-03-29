﻿// hardcodet.net NotifyIcon for WPF
// Copyright (c) 2009 - 2013 Philipp Sumi
// Contact and Information: http://www.hardcodet.net
//
// This library is free software; you can redistribute it and/or
// modify it under the terms of the Code Project Open License (CPOL);
// either version 1.0 of the License, or (at your option) any later
// version.
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
// THIS COPYRIGHT NOTICE MAY NOT BE REMOVED FROM THIS FILE

using Hardcodet.Wpf.TaskbarNotification.Interop;

using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Hardcodet.Wpf.TaskbarNotification
{
    /// <summary>
    /// Util and extension methods.
    /// </summary>
    internal static class Util
    {
        public static readonly Object SyncRoot = new Object();

        #region IsDesignMode

        /// <summary>
        /// Checks whether the application is currently in design mode.
        /// </summary>
        public static Boolean IsDesignMode { get; private set; }

        #endregion

        #region construction

        static Util()
        {
            IsDesignMode =
                (Boolean)
                    DependencyPropertyDescriptor.FromProperty( DesignerProperties.IsInDesignModeProperty,
                        typeof( FrameworkElement ) )
                        .Metadata.DefaultValue;
        }

        #endregion

        #region CreateHelperWindow

        /// <summary>
        /// Creates an transparent window without dimension that
        /// can be used to temporarily obtain focus and/or
        /// be used as a window message sink.
        /// </summary>
        /// <returns>Empty window.</returns>
        public static Window CreateHelperWindow() => new Window
        {
            Width = 0,
            Height = 0,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Opacity = 0
        };

        #endregion

        #region WriteIconData

        /// <summary>
        /// Updates the taskbar icons with data provided by a given
        /// <see cref="NOTIFYICONDATA"/> instance.
        /// </summary>
        /// <param name="data">Configuration settings for the NotifyIcon.</param>
        /// <param name="command">Operation on the icon (e.g. delete the icon).</param>
        /// <returns>True if the data was successfully written.</returns>
        /// <remarks>See Shell_NotifyIcon documentation on MSDN for details.</remarks>
        public static Boolean WriteIconData( ref NOTIFYICONDATA data, NotifyCommand command ) => WriteIconData( ref data, command, data.ValidMembers );

        /// <summary>
        /// Updates the taskbar icons with data provided by a given
        /// <see cref="NOTIFYICONDATA"/> instance.
        /// </summary>
        /// <param name="data">Configuration settings for the NotifyIcon.</param>
        /// <param name="command">Operation on the icon (e.g. delete the icon).</param>
        /// <param name="flags">Defines which members of the <paramref name="data"/>
        /// structure are set.</param>
        /// <returns>True if the data was successfully written.</returns>
        /// <remarks>See Shell_NotifyIcon documentation on MSDN for details.</remarks>
        public static Boolean WriteIconData( ref NOTIFYICONDATA data, NotifyCommand command, IconDataMembers flags )
        {
            //do nothing if in design mode
            if ( IsDesignMode )
            {
                return true;
            }

            data.ValidMembers = flags;
            lock ( SyncRoot )
            {
                return WinApi.Shell_NotifyIcon( command, ref data );
            }
        }

        #endregion

        #region GetBalloonFlag

        /// <summary>
        /// Gets a <see cref="BalloonIcons"/> enum value that
        /// matches a given <see cref="BalloonIcon"/>.
        /// </summary>
        public static BalloonIcons GetBalloonFlag( this BalloonIcon icon )
        {
            switch ( icon )
            {
                case BalloonIcon.None:
                    return BalloonIcons.None;
                case BalloonIcon.Info:
                    return BalloonIcons.Info;
                case BalloonIcon.Warning:
                    return BalloonIcons.Warning;
                case BalloonIcon.Error:
                    return BalloonIcons.Error;
                default:
                    throw new ArgumentOutOfRangeException( "icon" );
            }
        }

        #endregion

        #region ImageSource to Icon

        /// <summary>
        /// Reads a given image resource into a WinForms icon.
        /// </summary>
        /// <param name="imageSource">Image source pointing to
        /// an icon file (*.ico).</param>
        /// <returns>An icon object that can be used with the
        /// taskbar area.</returns>
        public static Icon ToIcon( this ImageSource imageSource )
        {
            if ( imageSource == null )
            {
                return null;
            }

            var uri = new Uri( imageSource.ToString() );
            System.Windows.Resources.StreamResourceInfo streamInfo = Application.GetResourceStream( uri );

            if ( streamInfo == null )
            {
                String msg = "The supplied image source '{0}' could not be resolved.";
                msg = String.Format( msg, imageSource );
                throw new ArgumentException( msg );
            }

            return new Icon( streamInfo.Stream );
        }

        #endregion

        #region evaluate listings

        /// <summary>
        /// Checks a list of candidates for equality to a given
        /// reference value.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The evaluated value.</param>
        /// <param name="candidates">A liste of possible values that are
        /// regarded valid.</param>
        /// <returns>True if one of the submitted <paramref name="candidates"/>
        /// matches the evaluated value. If the <paramref name="candidates"/>
        /// parameter itself is null, too, the method returns false as well,
        /// which allows to check with null values, too.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="candidates"/>
        /// is a null reference.</exception>
        private static Boolean Is<T>( this T value, params T[] candidates ) => candidates != null && candidates.Contains( value );

        #endregion

        #region match MouseEvent to PopupActivation

        /// <summary>
        /// Checks if a given <see cref="PopupActivationMode"/> is a match for
        /// an effectively pressed mouse button.
        /// </summary>
        public static Boolean IsMatch( this MouseEvent me, PopupActivationMode activationMode )
        {
            switch ( activationMode )
            {
                case PopupActivationMode.LeftClick:
                    return me == MouseEvent.IconLeftMouseUp;
                case PopupActivationMode.RightClick:
                    return me == MouseEvent.IconRightMouseUp;
                case PopupActivationMode.LeftOrRightClick:
                    return me.Is( MouseEvent.IconLeftMouseUp, MouseEvent.IconRightMouseUp );
                case PopupActivationMode.LeftOrDoubleClick:
                    return me.Is( MouseEvent.IconLeftMouseUp, MouseEvent.IconDoubleClick );
                case PopupActivationMode.DoubleClick:
                    return me.Is( MouseEvent.IconDoubleClick );
                case PopupActivationMode.MiddleClick:
                    return me == MouseEvent.IconMiddleMouseUp;
                case PopupActivationMode.All:
                    //return true for everything except mouse movements
                    return me != MouseEvent.MouseMove;
                default:
                    throw new ArgumentOutOfRangeException( "activationMode" );
            }
        }

        #endregion

        #region execute command

        /// <summary>
        /// Executes a given command if its <see cref="ICommand.CanExecute"/> method
        /// indicates it can run.
        /// </summary>
        /// <param name="command">The command to be executed, or a null reference.</param>
        /// <param name="commandParameter">An optional parameter that is associated with
        /// the command.</param>
        /// <param name="target">The target element on which to raise the command.</param>
        public static void ExecuteIfEnabled( this ICommand command, Object commandParameter, IInputElement target )
        {
            if ( command == null )
            {
                return;
            }

            if ( command is RoutedCommand rc )
            {
                //routed commands work on a target
                if ( rc.CanExecute( commandParameter, target ) )
                {
                    rc.Execute( commandParameter, target );
                }
            }
            else if ( command.CanExecute( commandParameter ) )
            {
                command.Execute( commandParameter );
            }
        }

        #endregion

        /// <summary>
        /// Returns a dispatcher for multi-threaded scenarios
        /// </summary>
        /// <returns></returns>
        internal static Dispatcher GetDispatcher( this DispatcherObject source )
        {
            //use the application's dispatcher by default
            if ( Application.Current != null )
            {
                return Application.Current.Dispatcher;
            }

            //fallback for WinForms environments
            if ( source.Dispatcher != null )
            {
                return source.Dispatcher;
            }

            //ultimatively use the thread's dispatcher
            return Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// Checks whether the <see cref="FrameworkElement.DataContextProperty"/>
        ///  is bound or not.
        /// </summary>
        /// <param name="element">The element to be checked.</param>
        /// <returns>True if the data context property is being managed by a
        /// binding expression.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="element"/>
        /// is a null reference.</exception>
        public static Boolean IsDataContextDataBound( this FrameworkElement element )
        {
            switch ( element )
            {
                case null:
                    throw new ArgumentNullException( "element" );
                default:
                    return element.GetBindingExpression( FrameworkElement.DataContextProperty ) != null;
            }
        }
    }
}