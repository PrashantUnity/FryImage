using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PdfEditorApp.Plugins.ImageEditor.Core.Raster;

namespace PdfEditorApp.Plugins.ImageEditor;

public partial class ImageEditorViewModel
{
    // ── Curves Modal State ────────────────────────────────────────────────
    [ObservableProperty] private string _adjCurvesChannel = "RGB"; // "RGB", "Red", "Green", "Blue"
    [ObservableProperty] private ObservableCollection<CurvePoint> _adjCurvesPointsRgb = new() { new(0, 0), new(255, 255) };
    [ObservableProperty] private ObservableCollection<CurvePoint> _adjCurvesPointsRed = new() { new(0, 0), new(255, 255) };
    [ObservableProperty] private ObservableCollection<CurvePoint> _adjCurvesPointsGreen = new() { new(0, 0), new(255, 255) };
    [ObservableProperty] private ObservableCollection<CurvePoint> _adjCurvesPointsBlue = new() { new(0, 0), new(255, 255) };

    [ObservableProperty] private double _adjCurvesSelectedIn = 0;
    [ObservableProperty] private double _adjCurvesSelectedOut = 0;
    [ObservableProperty] private bool _adjCurvesHasSelectedPoint;
    [ObservableProperty] private int _adjCurvesSelectedPointIndex = -1;

    public ObservableCollection<CurvePoint> ActiveCurvesPoints => AdjCurvesChannel switch
    {
        "Red" => AdjCurvesPointsRed,
        "Green" => AdjCurvesPointsGreen,
        "Blue" => AdjCurvesPointsBlue,
        _ => AdjCurvesPointsRgb
    };

    public IReadOnlyList<string> AvailableCurvesChannels { get; } = ["RGB", "Red", "Green", "Blue"];

    public IReadOnlyList<string> AvailableCurvesPresets { get; } =
        ["Default", "Medium Contrast", "High Contrast", "Linear Invert", "Lighter", "Darker"];

    [ObservableProperty] private string _selectedCurvesPreset = "Default";

    partial void OnAdjCurvesChannelChanged(string value)
    {
        AdjCurvesSelectedPointIndex = -1;
        AdjCurvesHasSelectedPoint = false;
        OnPropertyChanged(nameof(ActiveCurvesPoints));
    }

    partial void OnSelectedCurvesPresetChanged(string value)
    {
        ApplyCurvesPreset(value);
    }

    [RelayCommand]
    public void SelectCurvesPoint(CurvePoint pt)
    {
        AdjCurvesSelectedIn = pt.In;
        AdjCurvesSelectedOut = pt.Out;
        AdjCurvesHasSelectedPoint = true;
    }

    [RelayCommand]
    public void NotifyCurveChanged()
    {
        ApplyAdjustmentPreview();
    }

    [RelayCommand]
    public void ResetCurves()
    {
        AdjCurvesPointsRgb = new ObservableCollection<CurvePoint> { new(0, 0), new(255, 255) };
        AdjCurvesPointsRed = new ObservableCollection<CurvePoint> { new(0, 0), new(255, 255) };
        AdjCurvesPointsGreen = new ObservableCollection<CurvePoint> { new(0, 0), new(255, 255) };
        AdjCurvesPointsBlue = new ObservableCollection<CurvePoint> { new(0, 0), new(255, 255) };
        AdjCurvesSelectedPointIndex = -1;
        AdjCurvesHasSelectedPoint = false;
        OnPropertyChanged(nameof(ActiveCurvesPoints));
        ApplyAdjustmentPreview();
    }

    [RelayCommand]
    public void DeleteSelectedCurvePoint()
    {
        var pts = ActiveCurvesPoints;
        if (AdjCurvesSelectedPointIndex > 0 && AdjCurvesSelectedPointIndex < pts.Count - 1)
        {
            pts.RemoveAt(AdjCurvesSelectedPointIndex);
            AdjCurvesSelectedPointIndex = -1;
            AdjCurvesHasSelectedPoint = false;
            ApplyAdjustmentPreview();
        }
    }

    public void ApplyCurvesPreset(string preset)
    {
        var pts = ActiveCurvesPoints;
        pts.Clear();

        switch (preset)
        {
            case "Medium Contrast":
                pts.Add(new CurvePoint(0, 0));
                pts.Add(new CurvePoint(64, 52));
                pts.Add(new CurvePoint(192, 203));
                pts.Add(new CurvePoint(255, 255));
                break;

            case "High Contrast":
                pts.Add(new CurvePoint(0, 0));
                pts.Add(new CurvePoint(64, 40));
                pts.Add(new CurvePoint(192, 215));
                pts.Add(new CurvePoint(255, 255));
                break;

            case "Linear Invert":
                pts.Add(new CurvePoint(0, 255));
                pts.Add(new CurvePoint(255, 0));
                break;

            case "Lighter":
                pts.Add(new CurvePoint(0, 0));
                pts.Add(new CurvePoint(128, 160));
                pts.Add(new CurvePoint(255, 255));
                break;

            case "Darker":
                pts.Add(new CurvePoint(0, 0));
                pts.Add(new CurvePoint(128, 96));
                pts.Add(new CurvePoint(255, 255));
                break;

            case "Default":
            default:
                pts.Add(new CurvePoint(0, 0));
                pts.Add(new CurvePoint(255, 255));
                break;
        }

        AdjCurvesSelectedPointIndex = -1;
        AdjCurvesHasSelectedPoint = false;
        ApplyAdjustmentPreview();
    }

    public byte[] ExecuteCurvesAdjustment(byte[] bgra)
    {
        byte[] lutRgb = CurveSplineEngine.BuildLut(AdjCurvesPointsRgb);
        byte[] lutR = CurveSplineEngine.BuildLut(AdjCurvesPointsRed);
        byte[] lutG = CurveSplineEngine.BuildLut(AdjCurvesPointsGreen);
        byte[] lutB = CurveSplineEngine.BuildLut(AdjCurvesPointsBlue);

        byte[] finalLutR = new byte[256];
        byte[] finalLutG = new byte[256];
        byte[] finalLutB = new byte[256];

        for (int i = 0; i < 256; i++)
        {
            byte c = lutRgb[i];
            finalLutR[i] = lutR[c];
            finalLutG[i] = lutG[c];
            finalLutB[i] = lutB[c];
        }

        return ImageAdjustmentEngine.ApplyCurves(bgra, finalLutR, finalLutG, finalLutB);
    }
}
