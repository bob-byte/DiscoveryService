using LUC.Interfaces;

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition.Hosting;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.WpfClient
{
    internal class ExportedValueProviderAdapter : IExportValueProvider
    {
        private readonly ExportProvider m_exportProvider;

        public ExportedValueProviderAdapter(ExportProvider exportProvider)
        {
            m_exportProvider = exportProvider;
        }

        public T ExportedValue<T>() =>
            m_exportProvider.GetExportedValue<T>();
    }
}
