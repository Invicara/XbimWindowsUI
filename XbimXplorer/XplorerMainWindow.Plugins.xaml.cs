﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using NuGet;
using Xbim.Presentation.XplorerPluginSystem;
using XbimXplorer.PluginSystem;
using Xceed.Wpf.AvalonDock.Layout;

namespace XbimXplorer
{
    public partial class XplorerMainWindow
    {
        internal readonly bool PreventPluginLoad = false;

        public void BroadCastMessage(object sender, string messageTypeString, object messageData)
        {
            foreach (var window in _pluginWindows)
            {
                var msging = window as IXbimXplorerPluginMessageReceiver;
                msging?.ProcessMessage(sender, messageTypeString, messageData);
            }
        }

        public Visibility PluginMenuVisibility =>
            PluginMenu.Items.Count == 0
                ? Visibility.Collapsed
                : Visibility.Visible;

        public void RefreshPlugins()
        {
            var dirs = PluginManagement.GetPluginDirectories();
            foreach (var dir in dirs)
            {
                // evaluate the loading of the plugin from a folder
                LoadPlugin(dir, false);
            }
            PluginMenu.Visibility = PluginMenuVisibility;
        }

        private string _assemblyLoadFolder = "";

        /// <summary>
        /// key is ManifestMetadata.Id, data is ManifestMetadata
        /// </summary>
        private readonly Dictionary<string, ManifestMetadata> _loadedPlugins = new Dictionary<string, ManifestMetadata>();

        /// <summary>
        /// 
        /// </summary>
        /// <param name="dir"></param>
        /// <param name="forceLoad"></param>
        /// <param name="fileName"></param>
        /// <returns>True if plugin is completely loaded. False if not, for any reason.</returns>
        internal bool LoadPlugin(DirectoryInfo dir, bool forceLoad, string fileName = null)
        {
            var mfst = PluginManagement.GetManifestMetadata(dir);
            if (_loadedPlugins.ContainsKey(mfst.Id))
            {
                Log.Warn($"Re-load of previousely loaded plugin {mfst.Id} cancelled.");
                return false;
            }
            if (!forceLoad) // if don't have to load forcedly
            {
                // check startup setting
                //
                var conf = PluginManagement.GetConfiguration(dir);
                if (conf?.OnStartup != PluginConfiguration.StartupBehaviour.Enabled)
                    return false;
            }
            var fullAssemblyFileName = PluginManagement.GetEntryFile(dir, fileName);
            if (!File.Exists(fullAssemblyFileName))
            {
                Log.Error($"Plugin loading error: Assembly file not found [{fullAssemblyFileName}].");
                return false;
            }
            Log.InfoFormat("Attempting to load plugin: {0}", fullAssemblyFileName);
            _assemblyLoadFolder = dir.FullName;

            var assembly = LoadAssembly(fullAssemblyFileName);
            if (assembly == null)
                return false;
            _loadedPlugins.Add(mfst.Id, mfst);
            _pluginAssemblies.Add(assembly);

            var loadQueue = new Queue<AssemblyName>(assembly.GetReferencedAssemblies());

            while (loadQueue.Any())
            {
                var refReq = loadQueue.Dequeue();

                //check if the assembly is loaded
                var asms = AppDomain.CurrentDomain.GetAssemblies();
                var reqFound = false;
                foreach (var asmName in asms.Select(asm => asm.GetName()))
                {
                    if (asmName.FullName.Equals(refReq.FullName))
                    {
                        reqFound = true;
                        break;
                    }
                    if (asmName.Name.Equals(refReq.Name))
                    {
                        Log.DebugFormat("Versioning issues:\r\n" +
                                        "Required -> {0}\r\n" +
                                        "Loaded   -> {1}", refReq.FullName, asmName.FullName);
                    }
                }
                if (reqFound)
                    continue;

                AppDomain.CurrentDomain.AssemblyResolve += PluginAssemblyResolvingFunction;
                try
                {
                    var reqAss = Assembly.Load(refReq);
                    if (!_pluginAssemblies.Contains(reqAss))
                        _pluginAssemblies.Add(reqAss);
                    Log.DebugFormat("Loaded assembly: {0}", refReq.FullName);
                    foreach (var referenced in reqAss.GetReferencedAssemblies())
                    {
                        loadQueue.Enqueue(referenced);
                    }
                }
                catch (Exception ex)
                {
                    var msg = "Problem loading assembly " + refReq + " for " + fullAssemblyFileName;
                    Log.ErrorFormat(msg, ex);
                    MessageBox.Show(msg + ", " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                AppDomain.CurrentDomain.AssemblyResolve -= PluginAssemblyResolvingFunction;
            }
            var types = new List<Type>();
            try
            {
                types.AddRange(assembly.GetTypes()
                        .Where(i => i != null && typeof(UserControl).IsAssignableFrom(i) && i.Assembly == assembly)
                );
                types.AddRange(assembly.GetTypes()
                        .Where(i => i != null && typeof(Window).IsAssignableFrom(i) && i.Assembly == assembly)
                );
            }
            catch (ReflectionTypeLoadException ex)
            {
                foreach (var theType in ex.Types)
                {
                    try
                    {
                        if (theType != null && typeof(UserControl).IsAssignableFrom(theType) &&
                            theType.Assembly == assembly)
                            types.Add(theType);
                    }
                    // This exception list is not exhaustive, modify to suit any reasons
                    // you find for failure to parse a single assembly
                    catch (BadImageFormatException bfe)
                    {
                        Log.Error("Plugin error, bad format exception.", bfe);
                    }
                }
            }
            // set UI visibility
            try
            {
                foreach (var tp in types)
                {
                    EvaluateXbimUiType(tp);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error activating plugin {mfst.Id}; startup mode set to 'Ignore'.", ex);
                PluginManagement.SetStartup(dir, PluginConfiguration.StartupBehaviour.Disabled);
                return false;
            }
            PluginMenu.Visibility = PluginMenuVisibility;
            return true;
        }

        /// <summary>
        /// key is assembly full name, value is path to file
        /// </summary>
        private readonly Dictionary<string, string> _assemblyLocations = new Dictionary<string, string>();

        /// <summary>
        /// loads an assembly from a file.
        /// Loading happens through a memory stream to allow file deletion, while retaining the information of its location for the plugin API.
        /// </summary>
        /// <param name="fullAssemblyPath">Full path of the assembly to load.</param>
        /// <returns>the assembly or null in case of failure</returns>
        private Assembly LoadAssembly(string fullAssemblyPath)
        {
            // the use of Assembly.Load(File.ReadAllBytes(assemblypath)) is introduced to allow plugin files to be deleted.
            // this is required for the plugin update feature 
            //
            var loaded = Assembly.Load(File.ReadAllBytes(fullAssemblyPath));
            if (!_assemblyLocations.ContainsKey(loaded.FullName))
            {
                _assemblyLocations.Add(loaded.FullName, fullAssemblyPath);
            }
            return loaded;
        }

      private void EvaluateXbimUiType(Type type)
      {
         if (!typeof(IXbimXplorerPluginWindow).IsAssignableFrom(type))
         {
            return;
         }
         EvaluateXbimUiMenu(type);

         var act = type.GetUiActivation();
         if (act != PluginWindowActivation.OnLoad)
            return;
         var instance = Activator.CreateInstance(type);
         var asPWin = instance as IXbimXplorerPluginWindow;
         if (asPWin == null)
            return;
         if (_pluginWindows.Contains(asPWin))
            return;

         //(instance as UserControl).Loaded += PluginWindowLoaded;
         //(instance as UserControl).Unloaded += PluginWindowUnloaded;
         //(instance as UserControl).IsVisibleChanged += PluginWindowVisibleChanged;

         var menuWindow = ShowPluginWindow(asPWin, true);
         // if returned the window must be retained.
         var item = new SinglePluginItem()
         {
            PluginInterface = asPWin,
            UiObject = menuWindow
         };
         _retainedControls.Add(type, item);
         if ((_retainedControls[type].UiObject as LayoutAnchorable) != null)
         {
            (_retainedControls[type].UiObject as LayoutAnchorable).IsActive = true;
            //(_retainedControls[type].UiObject as LayoutAnchorable).Hiding += PluginWindowHiding;
         }
      }

        private void EvaluateXbimUiMenu(Type type)
        {
            var att = type.GetUiAttribute();
            if (string.IsNullOrEmpty(att?.MenuText)) 
                return;
            var destMenu = PluginMenu;
            var menuHeader = type.Name;
            if (!string.IsNullOrEmpty(att.MenuText))
            {
                menuHeader = att.MenuText;    
            }
            if (att.MenuText.StartsWith(@"View/Developer/"))
            {
                menuHeader = menuHeader.Substring(15);
                destMenu = DeveloperMenu;
            }
            if (att.MenuText.StartsWith(@"File/Export/"))
            {
                menuHeader = menuHeader.Substring(12);
                destMenu = ExportMenu;
            }

            var v = new MenuItem { Header = menuHeader, Tag = type };
            destMenu.Items.Add(v);
            v.Click += OpenPluginWindow;
        }

        private void OpenPluginWindow(object sender, RoutedEventArgs e)
        {
            var mi = sender as MenuItem;
            if (mi == null)
                return;
            OpenOrFocusPluginWindow(mi.Tag as Type);
        }
        
        private Assembly PluginAssemblyResolvingFunction(object sender, ResolveEventArgs args)
        {
            var parts = args.Name.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            var fName = Path.Combine(_assemblyLoadFolder, parts[0] + ".exe");
            if (File.Exists(fName))
            {
                return LoadAssembly(fName);
            }
            fName = Path.Combine(_assemblyLoadFolder, parts[0] + ".dll");
            return File.Exists(fName) 
                ? LoadAssembly(fName)
                : null;
        }
        
        private object ShowPluginWindow(IXbimXplorerPluginWindow pluginWindow, bool setCurrent = false)
        {
            // ReSharper disable once SuspiciousTypeConversion.Global
            var aswindow = pluginWindow as Window;
            if (aswindow != null)
            {
                
                var cmode = pluginWindow.GetUiContainerMode();
                if (cmode == PluginWindowUiContainerEnum.Dialog)
                {
                    pluginWindow.BindUi(MainWindow);
                    aswindow.ShowDialog();
                    var closeAction = pluginWindow.GetUiAttribute().CloseAction;
                    if (closeAction == PluginWindowCloseAction.Hide)
                        return aswindow;
                }
                else
                {
                    Log.ErrorFormat("Plugin type {0} has unsuitable containermode ({1}).", aswindow.GetType().Name, cmode);
                }
                return null;
            }

            var asControl = pluginWindow as UserControl;
            if (asControl != null)
            {
                  if (!_pluginWindows.Contains(pluginWindow))
                     _pluginWindows.Add(pluginWindow);
                  // preparing user control
                  asControl.HorizontalAlignment = HorizontalAlignment.Stretch;
                  asControl.VerticalAlignment = VerticalAlignment.Stretch;

                  //set data binding
                  pluginWindow.BindUi(MainWindow);

                  switch (pluginWindow.GetUiContainerMode())
                  {
                     case PluginWindowUiContainerEnum.LayoutAnchorable:
                     {
                        // inner 
                        var inner = new LayoutAnchorable()
                        {
                              Title = pluginWindow.WindowTitle,
                              Content = asControl
                        };

                        if (!GetRightPane().Children.Contains(inner))
                           GetRightPane().Children.Add(inner);

                        if (setCurrent)
                            inner.IsActive = true;
                        return inner;
                     }
                     case PluginWindowUiContainerEnum.LayoutDoc:
                     {
                        var ld = new LayoutDocument
                        {
                              Title = pluginWindow.WindowTitle,
                              Content = asControl
                        };
                        MainDocPane.Children.Add(ld);
                        ld.Closed += PluginWindowClosed;
                        if (setCurrent)
                              ld.IsActive = true;
                        return ld;
                     }
                     default:
                        Log.ErrorFormat("Plugin type {0} has unsuitable containermode.", asControl.GetType().Name);
                        break;
                  }
            }
            Log.ErrorFormat("{0} does not inherit from UserControl as expected", pluginWindow.GetType());
            return null;
         }
        
        LayoutAnchorablePaneGroup _rightPaneGroup;

        private LayoutAnchorablePaneGroup GetRightPaneGroup()
        {
            if (_rightPaneGroup != null && _rightPaneGroup.Parent != null)
                return _rightPaneGroup;
            _rightPaneGroup = new LayoutAnchorablePaneGroup
            {
                Orientation = Orientation.Vertical,
                DockMinWidth = 300
            };
            MainPanel.Children.Add(_rightPaneGroup);
            return _rightPaneGroup;
        }

        private LayoutAnchorablePane _rightPane;

        private LayoutAnchorablePane GetRightPane()
        {
            if (_rightPane != null && _rightPane.Parent != null)
                return _rightPane;
            var rigthPanel = GetRightPaneGroup();
            _rightPane = new LayoutAnchorablePane();
            rigthPanel.Children.Add(_rightPane);
            return _rightPane;
        }

        private class SinglePluginItem
        {
            public IXbimXplorerPluginWindow PluginInterface;
            public object UiObject;
        }

        // todo: do we need both the following?
        private readonly Dictionary<Type, SinglePluginItem> _retainedControls = new Dictionary<Type, SinglePluginItem>();
        private readonly PluginWindowCollection _pluginWindows = new PluginWindowCollection();
        private readonly List<Assembly> _pluginAssemblies = new List<Assembly>();

         private class PluginWindowCollection : ObservableCollection<IXbimXplorerPluginWindow>
         {
         }
  
         private void OpenOrFocusPluginWindow(Type tp)
         {
            bool pluginWindowIsInitialized = false;
            SinglePluginItem v = null;
            _retainedControls.TryGetValue(tp, out v);
            if (v != null)
            {
               var anchorableTest = v.UiObject as LayoutContent;
               // The plugin is already loaded in the memory. We need to figure out whether it is already loaded in the UI. There are a few things to check before it is certain that the plugin is still there
               // 1. From the Plugin itself whether it is Loaded and Visible
               var plgInW = anchorableTest.Content as Window;
               if (plgInW != null)
               {
                  if (plgInW.IsLoaded && plgInW.IsVisible)
                     return;     // It is already Loaded and Visible
               }
         
               var plgInUC = anchorableTest.Content as UserControl;
               if (plgInUC != null)
               {
                  if (plgInUC.IsLoaded && plgInUC.IsVisible)
                     return;     // It is already Loaded and Visible, or it is AutoHidden

                  if (anchorableTest.Parent is LayoutDocumentPane || anchorableTest.Parent is LayoutAnchorablePane)
                  {
                     // If it is docked under the LayoutDocumentPane or another LayoutAnchorablePane, it should be active there even though it might be Unloaded (e.g. when the Tab is not in focus)
                     return;
                  }

                  // Item might be not in focus as a tab item to any of the possible panel. Search whether it is there
                  if (plgInUC.Parent is Xceed.Wpf.AvalonDock.DockingManager)
                  {
                     Xceed.Wpf.AvalonDock.DockingManager dockMgr = plgInUC.Parent as Xceed.Wpf.AvalonDock.DockingManager;
                     if (dockMgr.Layout.Children.Count() > 0)
                     {
                        foreach (LayoutElement layoutElem in dockMgr.Layout.Children)
                        {
                           // Layout Panel may be recursive
                           if (layoutElem is LayoutPanel)
                              if (LocateControlInLayoutPanel(layoutElem as LayoutPanel, plgInUC))
                                 return;

                           if (layoutElem is LayoutAnchorSide)
                           {
                              foreach (LayoutAnchorGroup lyG in (layoutElem as LayoutAnchorSide).Children)
                              {
                                 foreach (LayoutAnchorable lyGA in lyG.Children)
                                 {
                                    if (lyGA.Content == plgInUC)
                                       return;     // The item is already in a LayoutAnchorGroup, probably is AutoHidden
                                 }
                              }
                           }
                        }
                     }
                  }
               }

               // If the status is not any of the above, show the plugin again
               ShowPluginWindow(v.PluginInterface, true);
            }
            else
            {
               IXbimXplorerPluginWindow instance;
               if (_retainedControls.ContainsKey(tp))
                  instance = _retainedControls[tp].PluginInterface;
               else
               {
                  try
                  {
                     instance = (IXbimXplorerPluginWindow) Activator.CreateInstance(tp);
                  }
                  catch (Exception ex)
                  {
                     var msg = $"Error creating instance of type '{tp}'";
                     Log.Error(msg, ex);
                     return;
                  }
               }
               var menuWindow = ShowPluginWindow(instance, true);
               if (menuWindow == null)
                  return;
               // if returned the window must be retained.
               var item = new SinglePluginItem()
               {
                  PluginInterface = instance,
                  UiObject = menuWindow
               };
               if (!_retainedControls.ContainsKey(tp))
                  _retainedControls.Add(tp, item);
               if ((_retainedControls[tp].UiObject as LayoutAnchorable) != null)
               {
                  (_retainedControls[tp].UiObject as LayoutAnchorable).IsActive = true;
               }
               return;
            }
         }

      private bool LocateControlInLayoutPanel (LayoutPanel layoutElem, UserControl controlToLocate)
      { 
         foreach (ILayoutPanelElement el in (layoutElem as LayoutPanel).Children)
         {
            if (el is LayoutPanel)
            {
               return LocateControlInLayoutPanel(el as LayoutPanel, controlToLocate);
            }

            if (el is LayoutDocumentPaneGroup)
            {
               foreach (ILayoutDocumentPane lPane in (el as LayoutDocumentPaneGroup).Children)
               {
                  foreach (LayoutContent la in lPane.Children)
                  {
                     if (la.Content == controlToLocate)
                        return true;
                  }
               }
            }

            if (el is LayoutDocumentPane)
            {
               foreach (LayoutContent lDoc in (el as LayoutDocumentPane).Children)
               {
                  if (lDoc.Content == controlToLocate)
                     return true;
               }
            }

            if (el is LayoutAnchorablePaneGroup)
            {
               foreach (ILayoutAnchorablePane lPane in (el as LayoutAnchorablePaneGroup).Children)
               {
                  foreach (LayoutAnchorable la in lPane.Children)
                  {
                     if (la.Content == controlToLocate)
                        return true;
                  }
               }
            }

            if (el is LayoutAnchorablePane)
            {
               var elP = el as LayoutAnchorablePane;
               foreach (LayoutAnchorable la in elP.Children)
               {
                  if (la.Content == controlToLocate)
                     return true;
               }
            }
         }

         return false;
      }

      public bool HasFloatingWindowChild()
      {
         if (this.MainPanel.Root.FloatingWindows.Count > 0)
            return true;

         return false;
      }


        private void PluginWindowClosed(object sender, EventArgs eventArgs)
        {
            IXbimXplorerPluginWindow vPlug = null;
            if (sender is LayoutAnchorable)
            {
                // nothing to do here, window is only hidden
                return;
            }
            // here we find the associated plugin item
            if (sender is LayoutDocument)
            {
                var cnt = ((LayoutDocument)sender).Content;
                vPlug = cnt as IXbimXplorerPluginWindow;
            }
            else if (sender is Window)
            {
                var cnt = (Window)sender;
                // ReSharper disable once SuspiciousTypeConversion.Global
                vPlug = cnt as IXbimXplorerPluginWindow;
            }
            if (vPlug == null)
                return;
            var tp = vPlug.GetType();
            var closeAction = vPlug.GetUiAttribute().CloseAction;

            if (closeAction == PluginWindowCloseAction.Close && _retainedControls.ContainsKey(tp) )
            {
                _retainedControls.Remove(tp);
            }
        }

        public string GetAssemblyLocation(Assembly requestingAssembly)
        {
            return _assemblyLocations.ContainsKey(requestingAssembly.FullName) 
                ? _assemblyLocations[requestingAssembly.FullName] 
                : null;
        }

        public string GetLoadedVersion(string pluginId)
        {
            return _loadedPlugins.ContainsKey(pluginId) 
                ? _loadedPlugins[pluginId].Version 
                : null;
        }
    }
}
