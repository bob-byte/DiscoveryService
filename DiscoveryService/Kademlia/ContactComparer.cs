using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LUC.DiscoveryService.Kademlia
{
    class ContactComparer : IEqualityComparer<Contact>
    {
        public Boolean Equals(Contact contact1, Contact contact2)
        {
            Validate.IsTrue<ArgumentNullException>(contact1 != null, errorMessage: $"{nameof(contact1)} is equal to null");
            Validate.IsTrue<ArgumentNullException>(contact2 != null, errorMessage: $"{nameof(contact2)} is equal to null");

            var isEqual = contact1.LocalEndPoints.Equals(contact2.LocalEndPoints);
            return isEqual;
        }

        public Int32 GetHashCode(Contact contact)
        {
            Validate.IsTrue<ArgumentNullException>(contact != null, errorMessage: $"{nameof(contact)} is equal to null");

            return contact.LocalEndPoints.GetHashCode();
        }
    }
}
