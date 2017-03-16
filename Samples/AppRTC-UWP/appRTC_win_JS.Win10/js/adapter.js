/*
 *  Copyright (c) 2014 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree.
 */

/* More information about these options at jshint.com/docs/options */
/* exported trace,requestUserMedia */

'use strict';
var attachMediaStream = null;
var reattachMediaStream = null;

// override logs with additional information
function trace(text) {
  if (text[text.length - 1] === '\n') {
    text = text.substring(0, text.length - 1);
  }
  if (window.performance) {
    var now = (window.performance.now() / 1000).toFixed(3);
    console.log(now + ': ' + text);
  } else {
    console.log(text);
  }
}
function alert(text) {
  trace(text);
}

if (Org.WebRtc) {
  console.log('This appears to be WinJS');

  // these are set to null in apprtc.debug.js, so override with ones set in webrtc_winJS_api.js
  attachMediaStream = navigator.attachMediaStream;
  reattachMediaStream = navigator.reattachMediaStream;
  
  // UI changes for AppRTC
  AppController.prototype.hide_ = function (element) {
    WinJS.Utilities.addClass(element, "hidden");
  };
  AppController.prototype.show_ = function (element) {
    WinJS.Utilities.removeClass(element, "hidden");
  };
  AppController.prototype.activate_ = function (element) {
    WinJS.Utilities.addClass(element, "active");
  };
  AppController.prototype.deactivate_ = function (element) {
    WinJS.Utilities.removeClass(element, "active");
  };
  AppController.IconSet_.prototype.toggle = function () {
    WinJS.Utilities.toggleClass(this.iconElement, "on");
  };
  AppController.prototype.toggleFullScreen_ = function () {
    // WinJS is always full screen so do nothing
  };

} else {
  console.log('Browser does not appear to be WebRTC-capable');
}

// returns the result of getUserMedia as a Promise
function requestUserMedia(constraints) {
  return navigator.getUserMedia(constraints).then(function (mediaStream) { return mediaStream; });
}
