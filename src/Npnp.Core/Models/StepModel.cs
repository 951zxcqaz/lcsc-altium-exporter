using System;

namespace Npnp.Core.Models;

/// <summary>
/// 3D 模型信息
/// </summary>
public record StepModel
{
    /// <summary>模型名称（如 "C0402"）</summary>
    public string Name { get; init; }

    /// <summary>通用 URL（向后兼容）</summary>
    public string Url { get; init; }

    /// <summary>STEP 模型的 UUID 或 URL</summary>
    public string? StepUrl { get; init; }

    /// <summary>OBJ 模型的 UUID 或 URL</summary>
    public string? ObjUrl { get; init; }

    /// <summary>已下载的二进制数据</summary>
    public byte[]? Data { get; init; }

    public StepModel(string name, string url, byte[]? data)
    {
        Name = name;
        Url = url;
        Data = data;
        StepUrl = url;
        ObjUrl = url;
    }

    public StepModel(string name, string url, string? stepUrl, string? objUrl, byte[]? data)
    {
        Name = name;
        Url = url;
        StepUrl = stepUrl;
        ObjUrl = objUrl;
        Data = data;
    }
}