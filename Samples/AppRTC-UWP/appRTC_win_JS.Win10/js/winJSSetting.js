//// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
//// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
//// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
//// PARTICULAR PURPOSE.
////
//// Copyright (c) Microsoft Corporation. All rights reserved

(function () {
  "use strict";
  var page = WinJS.UI.Pages.define("/default.html", {

    ready: function (element, options) {

      Org.WebRtc.WinJSHooks.requestAccessForMediaCapture().then(function (requested) {
        Org.WebRtc.WinJSHooks.initialize();
        setupDeviceSelectionUI();

        setupTraceUI();
 
      });
        
    }

  });

  function setupDeviceSelectionUI() {
    //setup devices list UI

    var localMedia = Org.WebRtc.Media.createMedia();

    var localAudioDevices = [];
    var audioDeviceList = localMedia.getAudioCaptureDevices();
    var selectedAudioDevice = -1;
    var selectedVideoDevice = -1;

    //construct audio list and mark user selected one
    for (var index = 0; index < audioDeviceList.length; index++) {
      var value = { id: audioDeviceList[index].id, name: audioDeviceList[index].name }
      if (window.localStorage.selectedAudio == value.name)
        selectedAudioDevice = index;
      localAudioDevices.push(value);
    }

    var localVideoDevices = [];
    var videoDeviceList = localMedia.getVideoCaptureDevices();

    //construct video list and mark user selected one
    for (var index = 0; index < videoDeviceList.length; index++) {
      var value = { id: videoDeviceList[index].id, name: videoDeviceList[index].name }
      if (window.localStorage.selectedVideo == value.name) {
        selectedVideoDevice = index;
      }
      localVideoDevices.push(value);
    }

    //bind data to UI
    var audioItemList = new WinJS.Binding.List(localAudioDevices)

    var audioDevices =
    {
      itemList: audioItemList
    };

    WinJS.Namespace.define("AudioDevices", audioDevices);

    document.getElementById("audioList").winControl.itemDataSource = audioDevices.itemList.dataSource;
    document.getElementById("audioList").winControl.selectionMode = WinJS.UI.SelectionMode.multi;
    document.getElementById("audioList").winControl.tapBehavior = WinJS.UI.TapBehavior.directSelect;


    if (selectedAudioDevice != -1) {
      document.getElementById("audioList").winControl.selection.set(selectedAudioDevice);
    }

    var videoItemList = new WinJS.Binding.List(localVideoDevices)

    var videoDevices =
    {
      itemList: videoItemList
    };

    WinJS.Namespace.define("videoDevices", videoDevices);

    document.getElementById("videoList").winControl.itemDataSource = videoDevices.itemList.dataSource;
    if (selectedVideoDevice != -1) {

      document.getElementById("videoList").winControl.selection.set(selectedVideoDevice);
    }

    document.getElementById("audioList").winControl.addEventListener("iteminvoked", handleAudioDeviceChanged);
    document.getElementById("videoList").winControl.addEventListener("iteminvoked", handleVideoDeviceChanged);
    document.getElementById("saveSettingButton").addEventListener("click", saveSetting);

  }

  function saveSetting() {
    var obj = $('#app_setting_panel')
    obj.classList.add('hidden');
  }

  function setupTraceUI() {


      document.getElementById("tracingSwitch").winControl.checked = Org.WebRtc.WinJSHooks.isTracing();
      document.getElementById("tracingSwitch").addEventListener("change", tracingToggled);

      toggleTraceServerControls(Org.WebRtc.WinJSHooks.isTracing())
    }

    function toggleTraceServerControls(enable) {
      document.getElementById("trace-server-ip").disabled = !enable;
      document.getElementById("trace-server-port").disabled = !enable;
    }

    function saveTrace() {
      var serverIp = document.getElementById("trace-server-ip").value;
      var serverPort = parseInt(document.getElementById("trace-server-port").value);
      Org.WebRtc.WinJSHooks.saveTrace(serverIp, serverPort);
    }

    function tracingToggled(evt) {
      var enabled = document.getElementById("tracingSwitch").winControl.checked;


      if (enabled) {
        Org.WebRtc.WinJSHooks.startTracing();
      }
      else {
        Org.WebRtc.WinJSHooks.stopTracing();
        saveTrace();
      }

      toggleTraceServerControls(enabled)
    }

    function handleAudioDeviceChanged(event) {
      var item = document.getElementById("audioList").winControl.itemDataSource.itemFromIndex(event.detail.itemIndex);

      if (!item)
        return;
      
  
      window.localStorage.selectedAudio = item._value.data.name;

    }

    function handleVideoDeviceChanged() {

      var item = document.getElementById("videoList").winControl.itemDataSource.itemFromIndex(event.detail.itemIndex);

      if (!item)
        return;
      window.localStorage.selectedVideo  = item._value.data.name;

    }

})();

