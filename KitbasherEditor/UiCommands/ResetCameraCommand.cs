﻿using CommonControls.Common.MenuSystem;
using CommonControls.Events.UiCommands;
using KitbasherEditor.ViewModels.MenuBarViews;
using System.Windows.Input;
using View3D.Services;

namespace KitbasherEditor.ViewModels.UiCommands
{
    public class ResetCameraCommand : IKitbasherUiCommand
    {
        public string ToolTip { get; set; } = "Reset camera";
        public ActionEnabledRule EnabledRule => ActionEnabledRule.Always;
        public Hotkey HotKey { get; } = new Hotkey(Key.F4, ModifierKeys.None);


        private readonly FocusSelectableObjectService _cameraFocusComponent;

        public ResetCameraCommand(FocusSelectableObjectService cameraFocusComponent)
        {
            _cameraFocusComponent = cameraFocusComponent;
        }

        public void Execute() => _cameraFocusComponent.ResetCamera();
    }
}
