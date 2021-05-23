using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Neo.UI
{
    public partial class PropertyGridForm : Form
    {
        public PropertyGridForm()
        {
            InitializeComponent();
        }

        internal void SetReadonly(object o, bool expandable = true)
        {
            propertyGrid1.SelectedObject = o;
            TypeDescriptor.AddAttributes(propertyGrid1.SelectedObject, new Attribute[] { new ReadOnlyAttribute(true) });
            if (expandable)
            {
                var attr = new TypeConverterAttribute(typeof(ExpandableObjectConverter));
                SetPropAttrRecursively(o.GetType(), attr);
            }


        }

        //TypeConverterAttribute _tcaColl = new TypeConverterAttribute(typeof(MyCollectionTypeDescriptor<,>));

        private void SetPropAttrRecursively(Type type, TypeConverterAttribute attr)
        {
            TypeDescriptor.AddAttributes(type, attr);
           /* if (type.IsAssignableFrom(typeof(Collection<>)))
                TypeDescriptor.AddAttributes(type, _tcaColl);*/

            foreach (var p in type.GetProperties())
                SetPropAttrRecursively(p.PropertyType, attr);

        }
    }


}
