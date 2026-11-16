using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SharpGLTF.Schema2;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Headless validation of the shipped GLB assets (no GraphicsDevice needed).</summary>
public class ModelAssetTests
{
    [Theory]
    [InlineData("Dog.glb")]
    [InlineData("Cat.glb")]
    public void PetModel_HasSkinAndLocomotionClips(string fileName)
    {
        string path = FindRepoFile(Path.Combine("Content", "Models3D", fileName));

        var model = ModelRoot.Load(path);

        Assert.NotEmpty(model.LogicalSkins);
        Assert.NotEmpty(model.LogicalMeshes);

        var clips = model.LogicalAnimations.Select(a => a.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Survey", clips);
        Assert.Contains("Walk", clips);
        Assert.Contains("Run", clips);
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
