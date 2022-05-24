using LUC.Interfaces;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Unity;

namespace LUC.IntegrationTests
{
    internal class ExportedValueProviderAdapter : IExportValueProvider
    {
        private readonly IUnityContainer m_exportProvider;

        public ExportedValueProviderAdapter( IUnityContainer exportProvider )
        {
            m_exportProvider = exportProvider;
        }

        public T ExportedValue<T>() =>
            m_exportProvider.Resolve<T>();
    }
}
