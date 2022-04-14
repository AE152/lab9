using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Models.TreeDataGrid;
using Avalonia.Controls.Selection;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using lab9.Models;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;

namespace lab9.ViewModels
{
    public class MainWindowViewModel : ViewModelBase
    {
        #region FIELDS

        private Tree? __rootDirectory;
        private string __selectedLogicalDrive;
        private string? __selectedPath;
        private Bitmap? __viewableImage = null;
        private bool __areButtonsActive = false;
        public IList<string> LogicalDrives { get; }
        public bool AreButtonsActive
        {
            get => __areButtonsActive;
            set => this.RaiseAndSetIfChanged(ref __areButtonsActive, value);
        }
        public string SelectedLogicalDrive
        {
            get => __selectedLogicalDrive;
            set => this.RaiseAndSetIfChanged(ref __selectedLogicalDrive, value);
        }
        public string? SelectedPath
        {
            get => __selectedPath;
            set => SetSelectedPath(value);
        }
        public Bitmap? ViewableImage
        {
            get => __viewableImage;
            set => this.RaiseAndSetIfChanged(ref __viewableImage, value);
        }
        public HierarchicalTreeDataGridSource<Tree> Source { get; }

        #endregion

        #region CONSTRUCTOR

        public MainWindowViewModel()
        {
            var assetLoader = AvaloniaLocator.Current.GetService<IAssetLoader>();
            LogicalDrives = DriveInfo.GetDrives().Select(x => x.Name).ToList();
            __selectedLogicalDrive = LogicalDrives[0].ToString();
            Source = new HierarchicalTreeDataGridSource<Tree>(Array.Empty<Tree>())
            {
                Columns =
                {
                    new HierarchicalExpanderColumn<Tree>(
                        new TemplateColumn<Tree>(
                            "Explorer",
                            new FuncDataTemplate<Tree>(FileNameTemplate, true),
                            new GridLength(1, GridUnitType.Star)),
                        x => x.Children,
                        x => x.IsExpanded)
                }
            };
            Source.RowSelection!.SingleSelect = false;
            Source.RowSelection.SelectionChanged += SelectionChanged;
            this.WhenAnyValue(x => x.SelectedLogicalDrive)
                .Subscribe(x =>
                {
                    __rootDirectory = new Tree(__selectedLogicalDrive, isRoot: true);
                    Source.Items = new[] { __rootDirectory };
                });
        }

        #endregion

        #region METHODS

        private IControl FileNameTemplate(Tree node, INameScope ns)
        {
            return new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children =
                {
                    new Image
                    {
                        [!Image.SourceProperty] = new MultiBinding
                        {
                            Bindings =
                            {
                                new Binding(nameof(node.IsExpanded)),
                            }
                        },
                        Margin = new Thickness(0, 0, 4, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                    },
                    new TextBlock
                    {
                        [!TextBlock.TextProperty] = new Binding(nameof(Tree.Name)),
                        VerticalAlignment = VerticalAlignment.Center,
                    }
                }
            };
        }
        private void SetSelectedPath(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                Source.RowSelection!.Clear();
                return;
            }
            var path = value;
            var components = new Stack<string>();
            DirectoryInfo? d = null;
            if (File.Exists(path))
            {
                var f = new FileInfo(path);
                components.Push(f.Name);
                d = f.Directory;
            }
            else if (Directory.Exists(path))
            {
                d = new DirectoryInfo(path);
            }
            while (d is not null)
            {
                components.Push(d.Name);
                d = d.Parent;
            }
            var index = IndexPath.Unselected;
            if (components.Count > 0)
            {
                var drive = components.Pop();
                var driveIndex = LogicalDrives.FindIndex(x => string.Equals(x, drive, StringComparison.OrdinalIgnoreCase));
                if (driveIndex >= 0)
                {
                    SelectedLogicalDrive = LogicalDrives[driveIndex];
                }
                Tree? node = __rootDirectory;
                index = new IndexPath(0);
                while (node is not null && components.Count > 0)
                {
                    node.IsExpanded = true;
                    var component = components.Pop();
                    var i = node.Children.FindIndex(x => string.Equals(x.Name, component, StringComparison.OrdinalIgnoreCase));
                    node = i >= 0 ? node.Children[i] : null;
                    index = i >= 0 ? index.Append(i) : default;
                }
            }
            Source.RowSelection!.SelectedIndex = index;
        }
        private void SelectionChanged(object? sender, TreeSelectionModelSelectionChangedEventArgs<Tree> e)
        {
            var selectedPath = Source.RowSelection?.SelectedItem?.Path;
            this.RaiseAndSetIfChanged(ref __selectedPath, selectedPath, nameof(SelectedPath));
            foreach (var i in e.DeselectedItems)
            {
                System.Diagnostics.Trace.WriteLine($"Deselected '{i?.Path}'");
            }
            foreach (var i in e.SelectedItems)
            {
                System.Diagnostics.Trace.WriteLine($"Selected '{i?.Path}'");
            }
            try
            {
                FileInfo fileInfo = new FileInfo(selectedPath);
                using (FileStream fs = fileInfo.OpenRead())
                {
                    try
                    {
                        ViewableImage = Bitmap.DecodeToWidth(fs, 500);
                        string mypath = selectedPath.Substring(0, selectedPath.LastIndexOf('\\'));
                        string[] files = Directory.GetFiles(mypath);
                        var jpgs = Array.FindAll(files, (fileName) => fileName.Contains(".jpg"));
                        if (jpgs.Length > 1)
                        {
                            AreButtonsActive = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        ViewableImage = null;
                        AreButtonsActive = false;
                    }
                }
            }
            catch
            {
                ViewableImage = null;
                AreButtonsActive = false;
            }
        }

        #endregion
    }
}

