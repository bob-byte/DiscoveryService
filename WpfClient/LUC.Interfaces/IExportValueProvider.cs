using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.Interfaces
{
    public interface IExportValueProvider
    {
        T ExportedValue<T>();
    }
}
