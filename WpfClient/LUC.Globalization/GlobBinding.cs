using System;
using System.Windows.Data;

namespace LUC.Globalization
{
    public class GlobBinding : Binding
    {
        public GlobBinding( String name ) : base( "[" + name + "]" )
        {
            Mode = BindingMode.OneWay;
            Source = TranslationSource.Instance;
        }
    }
}