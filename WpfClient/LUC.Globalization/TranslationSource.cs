using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace LUC.Globalization
{
    public class TranslationSource : INotifyPropertyChanged
    {
        public static readonly TranslationSource Instance = new TranslationSource();
        private readonly ResourceManager m_resManager = Strings.ResourceManager;
        private CultureInfo m_currentCulture;

        public System.String this[ System.String key ] => m_resManager.GetString( key, m_currentCulture );

        public CultureInfo CurrentCulture
        {
            get => m_currentCulture;
            set
            {
                if ( m_currentCulture != value )
                {
                    m_currentCulture = value;
                    PropertyChangedEventHandler @event = PropertyChanged;

                    if ( @event != null )
                    {
                        @event.Invoke( this, new PropertyChangedEventArgs( System.String.Empty ) );
                    }
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}