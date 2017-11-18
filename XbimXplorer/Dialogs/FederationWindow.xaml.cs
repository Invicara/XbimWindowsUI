/*
* @Author: Wawan Solihin (wawan.solihin@invicara.com)
* This is an additional class to support the simplified federaed model file .xbimf using Json file format and consists primarily the information about what files
* are included as members to the federation.
* The improvement involves support for a relative path so that the the federated set is portable
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
//using System.Windows.Forms;
using System.IO;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xbim.Presentation.FederatedModel;

namespace XbimXplorer.Dialogs
{
/// <summary>
/// Interaction logic for FederationWindow.xaml
/// </summary>
   public partial class FederationWindow : Window
   {
      IList<string> memberModels;
      public string federationName { get; set; }
      public string federationDescription { get; set; }
      public bool useRelativePath { get; set; }
      public bool IsCanceled = false;
      public string federationFileName;
      public bool isEdit = false;

      public FederationWindow()
      {
         InitializeComponent();
         memberModels = new List<string>();
         useRelativePath = false;
         listBox_FederationList.SelectionChanged += ListBox_FederationList_SelectionChanged;
         listBox_FederationList.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;
      }

      public FederationWindow(IList<string> initialMemberList)
      {
         InitializeComponent();
         memberModels = initialMemberList;
         listBox_FederationList.ItemsSource = memberModels;
         useRelativePath = false;
         listBox_FederationList.SelectionChanged += ListBox_FederationList_SelectionChanged;
         listBox_FederationList.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;
      }

      private void ListBox_FederationList_SelectionChanged(object sender, SelectionChangedEventArgs e)
      {
      }

      public IList<string> FederatedModelList
      {
         get
         {
            if (checkBox_RelativePath.IsChecked.Value == true)
               return memberModelsInAbsolutePath;
            else
               return memberModels;
         }
      }

      private void button_OKClick(object sender, RoutedEventArgs e)
      {
         //if (string.IsNullOrEmpty(textBox_Name.Text))
         //{
         //    textBox_Message.Text = "Federation Name must be defined!";
         //    return;
         //}
         //if (listBox_FederationList.Items.Count == 0)
         //{
         //    textBox_Message.Text = "Federation must have member models!";
         //    return;
         //}
         //federationName = textBox_Name.Text;
         //federationDescription = textBox_Description.Text;

         //if (!isEdit && File.Exists(textBox_FederatedFileName.Text))
         //{
         //    MessageBoxResult ret = MessageBox.Show("Federated File \"" + textBox_FederatedFileName.Text + "\" exists, do you want to overwrite?", 
         //        "Overwrite File?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
         //    if (ret == MessageBoxResult.No)
         //    {
         //        DialogResult = false;
         //        return;
         //    }
         //}

         //createFederationFile(textBox_FederatedFileName.Text);
         //federationFileName = textBox_FederatedFileName.Text;

         if (SaveFederationDefinition())
         {
            DialogResult = true;
            Close();
         }
      }

      private void createFederationFile(string fileName)
      {
         XBimFederation fedModel = new XBimFederation(federationName, federationDescription);
         fedModel.UseRelativePath = useRelativePath;
         foreach (string memberFile in listBox_FederationList.Items)
         {
            fedModel.Add(memberFile);
         }
         if (isEdit)
            fedModel.Save(fileName, firstTime:false);
         else
            fedModel.Save(fileName, firstTime: true);

      }

      private void button_Cancel_Click(object sender, RoutedEventArgs e)
      {
         IsCanceled = true;
         Close();
      }

      private void button_Add_Click(object sender, RoutedEventArgs e)
      {
         var dlg = new OpenFileDialog
         {
            Title = "Select model files to federate.",
            Filter = "Model Files|*.ifc;*.ifcxml;*.ifczip", // Filter files by extension 
            CheckFileExists = true,
            Multiselect = true
         };

         var done = dlg.ShowDialog(this);

         if (!done.Value)
            return;

         if (!dlg.FileNames.Any())
            return;

         IList<string> addedItems = new List<string>();
         int indexNo = memberModels.Count;
         foreach (string fileName in dlg.FileNames)
         {
            // Add only file that is not in the list (to avoid duplicates)
            if (!memberModels.Contains(fileName))
            {
               memberModels.Add(fileName);
               addedItems.Add(fileName);
            }
         }

         if (useRelativePath)
         {
            listBox_FederationList.ItemsSource = memberModelsInRelPath;
            foreach (string selItem in addedItems)
            {
               string selItemRelPath = Path.GetFileName(selItem);
               listBox_FederationList.SelectedItems.Add(selItemRelPath);
            }
         }
         else
         {
            listBox_FederationList.ItemsSource = memberModels;
            foreach (string selItem in addedItems)
               listBox_FederationList.SelectedItems.Add(selItem);
         }

         listBox_FederationList.Items.Refresh();
         textBox_Message.Clear();
      }

      private void button_Remove_Click(object sender, RoutedEventArgs e)
      {
         if (listBox_FederationList.SelectedItems.Count == 0)
         return;

      if (memberModels.Count == 0 && listBox_FederationList.Items.Count > 0)
      {
         foreach (string item in listBox_FederationList.Items)
            memberModels.Add(item);
      }

         if (useRelativePath)
         {
            foreach (string selItem in listBox_FederationList.SelectedItems)
            {
               memberModels.RemoveAt(memberModelsInRelPath.IndexOf(selItem));
            }
            listBox_FederationList.ItemsSource = memberModelsInRelPath;
         }
         else
         {
            foreach (string selItem in listBox_FederationList.SelectedItems)
               memberModels.Remove(selItem);
            listBox_FederationList.ItemsSource = memberModels;
         }
         listBox_FederationList.Items.Refresh();
      }

      private void checkBox_RelativePath_Checked(object sender, RoutedEventArgs e)
      {
         useRelativePath = true;
         listBox_FederationList.ItemsSource = memberModelsInRelPath;
         listBox_FederationList.Items.Refresh();
      }

      private void checkBox_RelativePath_Unchecked(object sender, RoutedEventArgs e)
      {
         useRelativePath = false;
         listBox_FederationList.ItemsSource = memberModelsInAbsolutePath;
         listBox_FederationList.Items.Refresh();
      }

      private void textBox_Message_TextChanged(object sender, TextChangedEventArgs e)
      {

      }

      private void textBox_Name_TextChanged(object sender, TextChangedEventArgs e)
      {
         if (!string.IsNullOrEmpty(textBox_Name.Text))
         {
            textBox_Message.Clear();
            string fedFolder;
            if (!string.IsNullOrEmpty(textBox_FederatedFileName.Text))
            {
               fedFolder = Path.GetDirectoryName(textBox_FederatedFileName.Text);
            }
            else
            {
               fedFolder = System.Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
            // Do not change the federated file name if it is in Edit mode
            if (!isEdit)
               textBox_FederatedFileName.Text = Path.Combine(fedFolder, textBox_Name.Text + ".xbimf");
         }
      }

      IList<string> memberModelsInAbsolutePath
      {
         get
         {
            string currDir = Directory.GetCurrentDirectory();
            string fedFileLoc = Path.GetDirectoryName(textBox_FederatedFileName.Text);
            Directory.SetCurrentDirectory(fedFileLoc);

            IList<string> mModelsInAbsPath = new List<string>();
            foreach (string model in memberModels)
            {
               string absFilePath = Path.GetFullPath(model);
               mModelsInAbsPath.Add(absFilePath);
            }
            Directory.SetCurrentDirectory(currDir);
            return mModelsInAbsPath;
         }
      }

      IList<string> memberModelsInRelPath
      {
         get
         {
            IList<string> mModelsInRelPath = new List<string>();
            foreach (string model in memberModels)
            {
               string relPath = XBimFederation.relativePath(Path.GetDirectoryName(textBox_FederatedFileName.Text), model);
               mModelsInRelPath.Add(relPath);
            }
            return mModelsInRelPath;
         }
      }

      private void textBox_FederatedFileName_TextChanged(object sender, TextChangedEventArgs e)
      {

      }

      private void button_BrowseFederatedFileName_Click(object sender, RoutedEventArgs e)
      {
         var dlg = new OpenFileDialog
         {
            Title = "Specify or select federated file name.",
            Filter = "Federated Model|*.xbimf", // Filter files by extension
            CheckFileExists = false,
            Multiselect = false
         };
         dlg.FileName = textBox_Name.Text;
         var done = dlg.ShowDialog(this);

         if (!done.Value)
         return;

         if (!dlg.FileName.Any())
         {
            if (string.IsNullOrEmpty(textBox_FederatedFileName.Text))
            {
               federationFileName = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "Default Federation";
            }
            else
               federationFileName = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + textBox_FederatedFileName.Text;
         }
         else
            federationFileName = dlg.FileName;

         textBox_FederatedFileName.Text = federationFileName;
      }

      private void button_SaveExit_Click(object sender, RoutedEventArgs e)
      {
         // Stay at the Dialog when there is error in saving
         if (SaveFederationDefinition())
         {
            IsCanceled = true;
            Close();
         }
      }

      private bool SaveFederationDefinition()
      {
         if (string.IsNullOrEmpty(textBox_Name.Text))
         {
            textBox_Message.Text = "Federation Name must be defined!";
            return false;
         }
         if (listBox_FederationList.Items.Count == 0)
         {
            textBox_Message.Text = "Federation must have member models!";
            return false;
         }
         federationName = textBox_Name.Text;
         federationDescription = textBox_Description.Text;

         if (!isEdit && File.Exists(textBox_FederatedFileName.Text))
         {
            MessageBoxResult ret = MessageBox.Show("Federated File \"" + textBox_FederatedFileName.Text + "\" exists, do you want to overwrite?",
                  "Overwrite File?", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
            if (ret == MessageBoxResult.No)
            {
               return false;
            }
         }

         createFederationFile(textBox_FederatedFileName.Text);
         federationFileName = textBox_FederatedFileName.Text;
         return true;
      }
   }
}
