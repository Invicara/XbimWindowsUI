using System;
using System.IO;
using System.Windows;
using Xbim.Ifc;

namespace XbimXplorer.Dialogs
{
    /// <summary>
    /// Interaction logic for ExportWindow.xaml
    /// </summary>
    public partial class InputWindow
    {
        public string ModelName {
            get {
                return this.txtModelName.Text;
            }
        }

        public InputWindow()
        {
            InitializeComponent();
        }
        
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(ModelName))
            {
                MessageBox.Show("Federation name cannot be empty!");
                return;
            }

            this.DialogResult = true;
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            txtModelName.SelectAll();
            txtModelName.Focus();
        }

    }
}
