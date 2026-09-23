using System;
using VeFoldery.Avalonia.ViewModels;
using VeFoldery.Core.Models;
using Xunit;

namespace VeFoldery.Core.Tests;

public class SelectAllTests
{
    private static FileEntryViewModel Entry(bool include) =>
        new FileEntryViewModel(new FileItem
        {
            FullPath = $"/tmp/x/{include}.txt",
            Name = $"{include}.txt",
            TargetFolder = "2026 September",
            ModifiedDate = DateTime.Now,
            Size = 10
        })
        { Include = include };

    [Fact]
    public void AllSelected_True_WhenAllIncluded()
    {
        var vm = new MainViewModel();
        vm.Files.Add(Entry(true));
        vm.Files.Add(Entry(true));
        Assert.True(vm.AllSelected);
    }

    [Fact]
    public void AllSelected_False_WhenNoneIncluded()
    {
        var vm = new MainViewModel();
        vm.Files.Add(Entry(false));
        Assert.False(vm.AllSelected!.Value);
    }

    [Fact]
    public void AllSelected_Null_WhenMixed()
    {
        var vm = new MainViewModel();
        vm.Files.Add(Entry(true));
        vm.Files.Add(Entry(false));
        Assert.Null(vm.AllSelected);
    }

    [Fact]
    public void ToggleAll_SelectsAndDeselects()
    {
        var vm = new MainViewModel();
        vm.Files.Add(Entry(false));
        vm.Files.Add(Entry(false));
        vm.ToggleAll();
        Assert.All(vm.Files, f => Assert.True(f.Include));
        Assert.True(vm.AllSelected);
        vm.ToggleAll();
        Assert.All(vm.Files, f => Assert.False(f.Include));
        Assert.False(vm.AllSelected!.Value);
    }

    [Fact]
    public void SettingAllSelected_PropagatesToRows()
    {
        var vm = new MainViewModel();
        vm.Files.Add(Entry(false));
        vm.Files.Add(Entry(true));
        vm.AllSelected = true;
        Assert.All(vm.Files, f => Assert.True(f.Include));
        vm.AllSelected = false;
        Assert.All(vm.Files, f => Assert.False(f.Include));
    }
}
