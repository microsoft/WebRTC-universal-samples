var Analytics = function (roomServer) {
  this.analyticsPath_ = roomServer + "/a/";
};

Analytics.EventObject_ = {};
Analytics.prototype.reportEvent = function (eventType, roomId, flowId) {
  var eventObj = {};
  eventObj[enums.RequestField.EventField.EVENT_TYPE] = eventType;
  eventObj[enums.RequestField.EventField.EVENT_TIME_MS] = Date.now();
  if (roomId) {
    eventObj[enums.RequestField.EventField.ROOM_ID] = roomId;
  }
  if (flowId) {
    eventObj[enums.RequestField.EventField.FLOW_ID] = flowId;
  }
  this.sendEventRequest_(eventObj);
};
Analytics.prototype.sendEventRequest_ = function (eventObj) {
  var request = {};
  request[enums.RequestField.TYPE] = enums.RequestField.MessageType.EVENT;
  request[enums.RequestField.REQUEST_TIME_MS] = Date.now();
  request[enums.RequestField.EVENT] = eventObj;
  sendAsyncUrlRequest("POST", this.analyticsPath_, JSON.stringify(request)).then(function () {
  }.bind(this), function (error) {
    trace("Failed to send event request: " + error.message);
  }.bind(this));
};
var enums = { "EventType": { "ICE_CONNECTION_STATE_CONNECTED": 3, "ROOM_SIZE_2": 2 }, "RequestField": { "MessageType": { "EVENT": "event" }, "CLIENT_TYPE": "client_type", "EventField": { "EVENT_TIME_MS": "event_time_ms", "ROOM_ID": "room_id", "EVENT_TYPE": "event_type", "FLOW_ID": "flow_id" }, "TYPE": "type", "EVENT": "event", "REQUEST_TIME_MS": "request_time_ms" }, "ClientType": { "UNKNOWN": 0, "ANDROID": 4, "DESKTOP": 2, "IOS": 3, "JS": 1 } };
var getUserMedia = null;
var attachMediaStream = null;
var reattachMediaStream = null;
var webrtcDetectedBrowser = null;
var webrtcDetectedVersion = null;
var webrtcMinimumVersion = null;
var webrtcUtils = {
  log: function () {
    if (!(typeof module !== "undefined" || typeof require === "function") && typeof define === "function") {
      console.log.apply(console, arguments);
    }
  }
};
function trace(text) {
  if (text[text.length - 1] === "\n") {
    text = text.substring(0, text.length - 1);
  }
  if (window.performance) {
    var now = (window.performance.now() / 1E3).toFixed(3);
    webrtcUtils.log(now + ": " + text);
  } else {
    webrtcUtils.log(text);
  }
}
if (typeof window === "undefined" || !window.navigator) {
  webrtcUtils.log("This does not appear to be a browser");
  webrtcDetectedBrowser = "not a browser";
} else {
  if (navigator.mozGetUserMedia) {
    webrtcUtils.log("This appears to be Firefox");
    webrtcDetectedBrowser = "firefox";
    webrtcDetectedVersion = parseInt(navigator.userAgent.match(/Firefox\/([0-9]+)\./)[1], 10);
    webrtcMinimumVersion = 31;
    window.RTCPeerConnection = function (pcConfig, pcConstraints) {
      if (webrtcDetectedVersion < 38) {
        if (pcConfig && pcConfig.iceServers) {
          var newIceServers = [];
          for (var i = 0; i < pcConfig.iceServers.length; i++) {
            var server = pcConfig.iceServers[i];
            if (server.hasOwnProperty("urls")) {
              for (var j = 0; j < server.urls.length; j++) {
                var newServer = { url: server.urls[j] };
                if (server.urls[j].indexOf("turn") === 0) {
                  newServer.username = server.username;
                  newServer.credential = server.credential;
                }
                newIceServers.push(newServer);
              }
            } else {
              newIceServers.push(pcConfig.iceServers[i]);
            }
          }
          pcConfig.iceServers = newIceServers;
        }
      }
      return new mozRTCPeerConnection(pcConfig, pcConstraints);
    };
    window.RTCSessionDescription = mozRTCSessionDescription;
    window.RTCIceCandidate = mozRTCIceCandidate;
    getUserMedia = function (constraints, onSuccess, onError) {
      var constraintsToFF37 = function (c) {
        if (typeof c !== "object" || c.require) {
          return c;
        }
        var require = [];
        Object.keys(c).forEach(function (key) {
          if (key === "require" || key === "advanced" || key === "mediaSource") {
            return;
          }
          var r = c[key] = typeof c[key] === "object" ? c[key] : { ideal: c[key] };
          if (r.min !== undefined || r.max !== undefined || r.exact !== undefined) {
            require.push(key);
          }
          if (r.exact !== undefined) {
            if (typeof r.exact === "number") {
              r.min = r.max = r.exact;
            } else {
              c[key] = r.exact;
            }
            delete r.exact;
          }
          if (r.ideal !== undefined) {
            c.advanced = c.advanced || [];
            var oc = {};
            if (typeof r.ideal === "number") {
              oc[key] = { min: r.ideal, max: r.ideal };
            } else {
              oc[key] = r.ideal;
            }
            c.advanced.push(oc);
            delete r.ideal;
            if (!Object.keys(r).length) {
              delete c[key];
            }
          }
        });
        if (require.length) {
          c.require = require;
        }
        return c;
      };
      if (webrtcDetectedVersion < 38) {
        webrtcUtils.log("spec: " + JSON.stringify(constraints));
        if (constraints.audio) {
          constraints.audio = constraintsToFF37(constraints.audio);
        }
        if (constraints.video) {
          constraints.video = constraintsToFF37(constraints.video);
        }
        webrtcUtils.log("ff37: " + JSON.stringify(constraints));
      }
      return navigator.mozGetUserMedia(constraints, onSuccess, onError);
    };
    navigator.getUserMedia = getUserMedia;
    if (!navigator.mediaDevices) {
      navigator.mediaDevices = {
        getUserMedia: requestUserMedia, addEventListener: function () {
        }, removeEventListener: function () {
        }
      };
    }
    navigator.mediaDevices.enumerateDevices = navigator.mediaDevices.enumerateDevices || function () {
      return new Promise(function (resolve) {
        var infos = [{ kind: "audioinput", deviceId: "default", label: "", groupId: "" }, { kind: "videoinput", deviceId: "default", label: "", groupId: "" }];
        resolve(infos);
      });
    };
    if (webrtcDetectedVersion < 41) {
      var orgEnumerateDevices = navigator.mediaDevices.enumerateDevices.bind(navigator.mediaDevices);
      navigator.mediaDevices.enumerateDevices = function () {
        return orgEnumerateDevices().catch(function (e) {
          if (e.name === "NotFoundError") {
            return [];
          }
          throw e;
        });
      };
    }
    attachMediaStream = function (element, stream) {
      element.mozSrcObject = stream;
    };
    reattachMediaStream = function (to, from) {
      to.mozSrcObject = from.mozSrcObject;
    };
  } else {
    if (navigator.webkitGetUserMedia) {
      webrtcUtils.log("This appears to be Chrome");
      webrtcDetectedBrowser = "chrome";
      webrtcDetectedVersion = parseInt(navigator.userAgent.match(/Chrom(e|ium)\/([0-9]+)\./)[2], 10);
      webrtcMinimumVersion = 38;
      window.RTCPeerConnection = function (pcConfig, pcConstraints) {
        if (pcConfig && pcConfig.iceTransportPolicy) {
          pcConfig.iceTransports = pcConfig.iceTransportPolicy;
        }
        var pc = new webkitRTCPeerConnection(pcConfig, pcConstraints);
        var origGetStats = pc.getStats.bind(pc);
        pc.getStats = function (selector, successCallback, errorCallback) {
          var self = this;
          var args = arguments;
          if (arguments.length > 0 && typeof selector === "function") {
            return origGetStats(selector, successCallback);
          }
          var fixChromeStats = function (response) {
            var standardReport = {};
            var reports = response.result();
            reports.forEach(function (report) {
              var standardStats = { id: report.id, timestamp: report.timestamp, type: report.type };
              report.names().forEach(function (name) {
                standardStats[name] = report.stat(name);
              });
              standardReport[standardStats.id] = standardStats;
            });
            return standardReport;
          };
          if (arguments.length >= 2) {
            var successCallbackWrapper = function (response) {
              args[1](fixChromeStats(response));
            };
            return origGetStats.apply(this, [successCallbackWrapper, arguments[0]]);
          }
          return new Promise(function (resolve, reject) {
            origGetStats.apply(self, [resolve, reject]);
          });
        };
        return pc;
      };
      ["createOffer", "createAnswer"].forEach(function (method) {
        var nativeMethod = webkitRTCPeerConnection.prototype[method];
        webkitRTCPeerConnection.prototype[method] = function () {
          var self = this;
          if (arguments.length < 1 || arguments.length === 1 && typeof arguments[0] === "object") {
            var opts = arguments.length === 1 ? arguments[0] : undefined;
            return new Promise(function (resolve, reject) {
              nativeMethod.apply(self, [resolve, reject, opts]);
            });
          } else {
            return nativeMethod.apply(this, arguments);
          }
        };
      });
      ["setLocalDescription", "setRemoteDescription", "addIceCandidate"].forEach(function (method) {
        var nativeMethod = webkitRTCPeerConnection.prototype[method];
        webkitRTCPeerConnection.prototype[method] = function () {
          var args = arguments;
          var self = this;
          return new Promise(function (resolve, reject) {
            nativeMethod.apply(self, [args[0], function () {
              resolve();
              if (args.length >= 2) {
                args[1].apply(null, []);
              }
            }, function (err) {
              reject(err);
              if (args.length >= 3) {
                args[2].apply(null, [err]);
              }
            }]);
          });
        };
      });
      var constraintsToChrome = function (c) {
        if (typeof c !== "object" || c.mandatory || c.optional) {
          return c;
        }
        var cc = {};
        Object.keys(c).forEach(function (key) {
          if (key === "require" || key === "advanced" || key === "mediaSource") {
            return;
          }
          var r = typeof c[key] === "object" ? c[key] : { ideal: c[key] };
          if (r.exact !== undefined && typeof r.exact === "number") {
            r.min = r.max = r.exact;
          }
          var oldname = function (prefix, name) {
            if (prefix) {
              return prefix + name.charAt(0).toUpperCase() + name.slice(1);
            }
            return name === "deviceId" ? "sourceId" : name;
          };
          if (r.ideal !== undefined) {
            cc.optional = cc.optional || [];
            var oc = {};
            if (typeof r.ideal === "number") {
              oc[oldname("min", key)] = r.ideal;
              cc.optional.push(oc);
              oc = {};
              oc[oldname("max", key)] = r.ideal;
              cc.optional.push(oc);
            } else {
              oc[oldname("", key)] = r.ideal;
              cc.optional.push(oc);
            }
          }
          if (r.exact !== undefined && typeof r.exact !== "number") {
            cc.mandatory = cc.mandatory || {};
            cc.mandatory[oldname("", key)] = r.exact;
          } else {
            ["min", "max"].forEach(function (mix) {
              if (r[mix] !== undefined) {
                cc.mandatory = cc.mandatory || {};
                cc.mandatory[oldname(mix, key)] = r[mix];
              }
            });
          }
        });
        if (c.advanced) {
          cc.optional = (cc.optional || []).concat(c.advanced);
        }
        return cc;
      };
      getUserMedia = function (constraints, onSuccess, onError) {
        if (constraints.audio) {
          constraints.audio = constraintsToChrome(constraints.audio);
        }
        if (constraints.video) {
          constraints.video = constraintsToChrome(constraints.video);
        }
        webrtcUtils.log("chrome: " + JSON.stringify(constraints));
        return navigator.webkitGetUserMedia(constraints, onSuccess, onError);
      };
      navigator.getUserMedia = getUserMedia;
      if (!navigator.mediaDevices) {
        navigator.mediaDevices = {
          getUserMedia: requestUserMedia, enumerateDevices: function () {
            return new Promise(function (resolve) {
              var kinds = { audio: "audioinput", video: "videoinput" };
              return MediaStreamTrack.getSources(function (devices) {
                resolve(devices.map(function (device) {
                  return { label: device.label, kind: kinds[device.kind], deviceId: device.id, groupId: "" };
                }));
              });
            });
          }
        };
      }
      if (!navigator.mediaDevices.getUserMedia) {
        navigator.mediaDevices.getUserMedia = function (constraints) {
          return requestUserMedia(constraints);
        };
      } else {
        var origGetUserMedia = navigator.mediaDevices.getUserMedia.bind(navigator.mediaDevices);
        navigator.mediaDevices.getUserMedia = function (c) {
          webrtcUtils.log("spec:   " + JSON.stringify(c));
          c.audio = constraintsToChrome(c.audio);
          c.video = constraintsToChrome(c.video);
          webrtcUtils.log("chrome: " + JSON.stringify(c));
          return origGetUserMedia(c);
        };
      }
      if (typeof navigator.mediaDevices.addEventListener === "undefined") {
        navigator.mediaDevices.addEventListener = function () {
          webrtcUtils.log("Dummy mediaDevices.addEventListener called.");
        };
      }
      if (typeof navigator.mediaDevices.removeEventListener === "undefined") {
        navigator.mediaDevices.removeEventListener = function () {
          webrtcUtils.log("Dummy mediaDevices.removeEventListener called.");
        };
      }
      attachMediaStream = function (element, stream) {
        if (typeof element.srcObject !== "undefined") {
          element.srcObject = stream;
        } else {
          if (typeof element.src !== "undefined") {
            element.src = URL.createObjectURL(stream);
          } else {
            webrtcUtils.log("Error attaching stream to element.");
          }
        }
      };
      reattachMediaStream = function (to, from) {
        to.src = from.src;
      };
    } else {
      if (navigator.mediaDevices && navigator.userAgent.match(/Edge\/(\d+).(\d+)$/)) {
        webrtcUtils.log("This appears to be Edge");
        webrtcDetectedBrowser = "edge";
        webrtcDetectedVersion = parseInt(navigator.userAgent.match(/Edge\/(\d+).(\d+)$/)[2], 10);
        webrtcMinimumVersion = 12;
        getUserMedia = navigator.getUserMedia;
        attachMediaStream = function (element, stream) {
          element.srcObject = stream;
        };
        reattachMediaStream = function (to, from) {
          to.srcObject = from.srcObject;
        };
      } else {
        webrtcUtils.log("Browser does not appear to be WebRTC-capable");
      }
    }
  }
}
function requestUserMedia(constraints) {
  return new Promise(function (resolve, reject) {
    getUserMedia(constraints, resolve, reject);
  });
}
var webrtcTesting = {};
Object.defineProperty(webrtcTesting, "version", {
  set: function (version) {
    webrtcDetectedVersion = version;
  }
});
if (typeof module !== "undefined") {
  var RTCPeerConnection;
  if (typeof window !== "undefined") {
    RTCPeerConnection = window.RTCPeerConnection;
  }
  module.exports = { RTCPeerConnection: RTCPeerConnection, getUserMedia: getUserMedia, attachMediaStream: attachMediaStream, reattachMediaStream: reattachMediaStream, webrtcDetectedBrowser: webrtcDetectedBrowser, webrtcDetectedVersion: webrtcDetectedVersion, webrtcMinimumVersion: webrtcMinimumVersion, webrtcTesting: webrtcTesting };
} else {
  if (typeof require === "function" && typeof define === "function") {
    define([], function () {
      return { RTCPeerConnection: window.RTCPeerConnection, getUserMedia: getUserMedia, attachMediaStream: attachMediaStream, reattachMediaStream: reattachMediaStream, webrtcDetectedBrowser: webrtcDetectedBrowser, webrtcDetectedVersion: webrtcDetectedVersion, webrtcMinimumVersion: webrtcMinimumVersion, webrtcTesting: webrtcTesting };
    });
  }
}
; var remoteVideo = $("#remote-video");
var UI_CONSTANTS = {
  confirmJoinButton: "#confirm-join-button", confirmJoinDiv: "#confirm-join-div", confirmJoinRoomSpan: "#confirm-join-room-span", fullscreenSvg: "#fullscreen", hangupSvg: "#hangup", settingSvg: "#setting", icons: "#icons", infoDiv: "#info-div", localVideo: "#local-video", miniVideo: "#mini-video", muteAudioSvg: "#mute-audio", muteVideoSvg: "#mute-video", newRoomButton: "#new-room-button", newRoomLink: "#new-room-link", remoteVideo: "#remote-video", rejoinButton: "#rejoin-button", rejoinDiv: "#rejoin-div", rejoinLink: "#rejoin-link",
  roomLinkHref: "#room-link-href", roomSelectionDiv: "#room-selection", roomSelectionInput: "#room-id-input", roomSelectionInputLabel: "#room-id-input-label", roomSelectionJoinButton: "#join-button", settingButton: "#setting-button", roomSelectionRandomButton: "#random-button", roomSelectionRecentList: "#recent-rooms-list", sharingDiv: "#sharing-div", statusDiv: "#status-div", videosDiv: "#videos"
};

var AppController = function (loadingParams) {
  trace("Initializing; server= " + loadingParams.roomServer + ".");
  trace("Initializing; room=" + loadingParams.roomId + ".");
  this.hangupSvg_ = $(UI_CONSTANTS.hangupSvg);
  this.settingSvg_ = $(UI_CONSTANTS.settingSvg);
  this.icons_ = $(UI_CONSTANTS.icons);
  this.localVideo_ = $(UI_CONSTANTS.localVideo);
  this.miniVideo_ = $(UI_CONSTANTS.miniVideo);
  this.sharingDiv_ = $(UI_CONSTANTS.sharingDiv);
  this.statusDiv_ = $(UI_CONSTANTS.statusDiv);
  this.remoteVideo_ = $(UI_CONSTANTS.remoteVideo);
  this.videosDiv_ = $(UI_CONSTANTS.videosDiv);
  this.roomLinkHref_ = $(UI_CONSTANTS.roomLinkHref);
  this.rejoinDiv_ = $(UI_CONSTANTS.rejoinDiv);
  this.rejoinLink_ = $(UI_CONSTANTS.rejoinLink);
  this.newRoomLink_ = $(UI_CONSTANTS.newRoomLink);
  this.rejoinButton_ = $(UI_CONSTANTS.rejoinButton);
  this.newRoomButton_ = $(UI_CONSTANTS.newRoomButton);
  this.newRoomButton_.addEventListener("click", this.onNewRoomClick_.bind(this), false);
  this.rejoinButton_.addEventListener("click", this.onRejoinClick_.bind(this), false);
  this.muteAudioIconSet_ = new AppController.IconSet_(UI_CONSTANTS.muteAudioSvg);
  this.muteVideoIconSet_ = new AppController.IconSet_(UI_CONSTANTS.muteVideoSvg);
  this.fullscreenIconSet_ = new AppController.IconSet_(UI_CONSTANTS.fullscreenSvg);
  this.loadingParams_ = loadingParams;
  this.loadUrlParams_();
  if (Org.WebRtc) {
    var app = WinJS.Application;

    var promiseErrhandled = false;
    app.addEventListener("checkpoint", this.onWinRTAppSuspending_.bind(this), false);

    Promise.prototype.catchDisp = function (value) {

      window.setTimeout(function () {
        //we only need to handle promise error once, as it is a serious error, we are going to reload the app
        // so that user may rejoin the room
        if (promiseErrhandled) {
          return;
        }

        promiseErrHappened = true;

        app.alert("Sorry, can not join this meeting room.\nplease try a different room.\n" + value, function () {
          Org.WebRtc.Media.onAppSuspending();
          window.location = "/default.html"; //reload
        });

      }, 1000);

    }

  }
  var paramsPromise = Promise.resolve({});
  if (this.loadingParams_.paramsFunction) {
    paramsPromise = this.loadingParams_.paramsFunction();
  }
  Promise.resolve(paramsPromise).then(function (newParams) {
    if (newParams) {
      Object.keys(newParams).forEach(function (key) {
        this.loadingParams_[key] = newParams[key];
      }.bind(this));
    }
    this.roomLink_ = "";
    this.roomSelection_ = null;
    this.localStream_ = null;
    this.remoteVideoResetTimer_ = null;
    if (this.loadingParams_.roomId) {
      this.createCall_();
      if (!RoomSelection.matchRandomRoomPattern(this.loadingParams_.roomId)) {
        $(UI_CONSTANTS.confirmJoinRoomSpan).textContent = ' "' + this.loadingParams_.roomId + '"';
      }
      var confirmJoinDiv = $(UI_CONSTANTS.confirmJoinDiv);
      this.show_(confirmJoinDiv);
      $(UI_CONSTANTS.confirmJoinButton).onclick = function () {
        this.hide_(confirmJoinDiv);
        var recentlyUsedList = new RoomSelection.RecentlyUsedList;
        recentlyUsedList.pushRecentRoom(this.loadingParams_.roomId);
        this.finishCallSetup_(this.loadingParams_.roomId);
      }.bind(this);
      if (this.loadingParams_.bypassJoinConfirmation) {
        $(UI_CONSTANTS.confirmJoinButton).onclick();
      }
    } else {
      this.showRoomSelection_();
    }
  }.bind(this)).catch(function (error) {
    trace("Error initializing: " + error.message);
  }.bind(this));
};

AppController.prototype.onWinRTAppSuspending_ = function (evt) {
  evt.detail.setPromise(this.processWinRTAppSuspending_());
}
AppController.prototype.processWinRTAppSuspending_ = function () {

  if (this.call_ != null) {
    this.call_.onWinRTAppSuspending();
  }
  Org.WebRtc.Media.onAppSuspending();
  window.location = "/default.html"; //reload
}

AppController.prototype.createCall_ = function () {
  this.call_ = new Call(this.loadingParams_);
  this.infoBox_ = new InfoBox($(UI_CONSTANTS.infoDiv), this.remoteVideo_, this.call_, this.loadingParams_.versionInfo);
  var roomErrors = this.loadingParams_.errorMessages;
  if (roomErrors && roomErrors.length > 0) {
    for (var i = 0; i < roomErrors.length; ++i) {
      this.infoBox_.pushErrorMessage(roomErrors[i]);
    }
    return;
  }
  this.call_.onremotehangup = this.onRemoteHangup_.bind(this);
  this.call_.onremotesdpset = this.onRemoteSdpSet_.bind(this);
  this.call_.onremotestreamadded = this.onRemoteStreamAdded_.bind(this);
  this.call_.onlocalstreamadded = this.onLocalStreamAdded_.bind(this);
  this.call_.onsignalingstatechange = this.infoBox_.updateInfoDiv.bind(this.infoBox_);
  this.call_.oniceconnectionstatechange = this.infoBox_.updateInfoDiv.bind(this.infoBox_);
  this.call_.onnewicecandidate = this.infoBox_.recordIceCandidateTypes.bind(this.infoBox_);
  this.call_.onerror = this.displayError_.bind(this);
  this.call_.onstatusmessage = this.displayStatus_.bind(this);
  this.call_.oncallerstarted = this.displaySharingInfo_.bind(this);
};
AppController.prototype.showRoomSelection_ = function () {
  var roomSelectionDiv = $(UI_CONSTANTS.roomSelectionDiv);
  this.roomSelection_ = new RoomSelection(roomSelectionDiv, UI_CONSTANTS);
  this.show_(roomSelectionDiv);
  this.roomSelection_.onRoomSelected = function (roomName) {
    this.hide_(roomSelectionDiv);
    this.createCall_();
    this.finishCallSetup_(roomName);
    this.roomSelection_.removeEventListeners();
    this.roomSelection_ = null;
    if (this.localStream_) {
      this.attachLocalStream_();
    }
  }.bind(this);
};
AppController.prototype.finishCallSetup_ = function (roomId) {
  this.call_.start(roomId);
  document.onkeypress = this.onKeyPress_.bind(this);
  window.onmousemove = this.showIcons_.bind(this);
  $(UI_CONSTANTS.muteAudioSvg).onclick = this.toggleAudioMute_.bind(this);
  $(UI_CONSTANTS.muteVideoSvg).onclick = this.toggleVideoMute_.bind(this);
  $(UI_CONSTANTS.fullscreenSvg).onclick = this.toggleFullScreen_.bind(this);
  $(UI_CONSTANTS.hangupSvg).onclick = this.hangup_.bind(this);
  $(UI_CONSTANTS.settingSvg).onclick = this.setting_.bind(this);
  setUpFullScreen();
  if (!isChromeApp()) {
    window.onbeforeunload = function () {
      this.call_.hangup(false);
    }.bind(this);
    window.onpopstate = function (event) {
      if (!event.state) {
        trace("Reloading main page.");
        location.href = location.origin;
      } else {
        if (event.state.roomLink) {
          location.href = event.state.roomLink;
        }
      }
    };
  }
};
AppController.prototype.hangup_ = function () {
  trace("Hanging up.");
  this.hide_(this.icons_);
  this.displayStatus_("Hanging up");
  this.transitionToDone_();
  this.call_.hangup(true);
};

AppController.prototype.setting_ = function () {

  var obj = $('#app_setting_panel')
  if (obj.classList.contains('hidden')) {
    //show setting

    obj.classList.remove('hidden');

  }

};

AppController.prototype.onRemoteHangup_ = function () {
  this.displayStatus_("The remote side hung up.");
  this.transitionToWaiting_();
  this.call_.onRemoteHangup();
};
AppController.prototype.onRemoteSdpSet_ = function (hasRemoteVideo) {
  if (hasRemoteVideo) {
    trace("Waiting for remote video.");
    this.waitForRemoteVideo_();
  } else {
    trace("No remote video stream; not waiting for media to arrive.");
    this.transitionToActive_();
  }
};
AppController.prototype.waitForRemoteVideo_ = function () {
  if (this.remoteVideo_.readyState >= 2) {
    trace("Remote video started; currentTime: " + this.remoteVideo_.currentTime);
    this.transitionToActive_();
  } else {
    this.remoteVideo_.oncanplay = this.waitForRemoteVideo_.bind(this);
  }
};
AppController.prototype.onRemoteStreamAdded_ = function (stream) {
  this.deactivate_(this.sharingDiv_);
  trace("Remote stream added.");
  attachMediaStream(this.remoteVideo_, stream);
  if (this.remoteVideoResetTimer_) {
    clearTimeout(this.remoteVideoResetTimer_);
    this.remoteVideoResetTimer_ = null;
  }
};
AppController.prototype.onLocalStreamAdded_ = function (stream) {
  trace("User has granted access to local media.");
  this.localStream_ = stream;
  if (!this.roomSelection_) {
    this.attachLocalStream_();
  }
};
AppController.prototype.attachLocalStream_ = function () {
  trace("Attaching local stream.");
  attachMediaStream(this.localVideo_, this.localStream_);
  this.displayStatus_("");
  this.activate_(this.localVideo_);
  this.show_(this.icons_);
};
AppController.prototype.transitionToActive_ = function () {
  this.remoteVideo_.oncanplay = undefined;
  var connectTime = window.performance.now();
  this.infoBox_.setSetupTimes(this.call_.startTime, connectTime);
  this.infoBox_.updateInfoDiv();
  trace("Call setup time: " + (connectTime - this.call_.startTime).toFixed(0) + "ms.");
  trace("reattachMediaStream: " + this.localVideo_.src);
  reattachMediaStream(this.miniVideo_, this.localVideo_);
  this.activate_(this.remoteVideo_);
  this.activate_(this.miniVideo_);
  this.deactivate_(this.localVideo_);
  this.localVideo_.src = "";
  this.activate_(this.videosDiv_);
  this.show_(this.hangupSvg_);
  this.displayStatus_("");
};
AppController.prototype.transitionToWaiting_ = function () {
  this.remoteVideo_.oncanplay = undefined;
  this.hide_(this.hangupSvg_);
  this.deactivate_(this.videosDiv_);
  if (!this.remoteVideoResetTimer_) {
    this.remoteVideoResetTimer_ = setTimeout(function () {
      this.remoteVideoResetTimer_ = null;
      trace("Resetting remoteVideo src after transitioning to waiting.");
      this.remoteVideo_.src = "";
    }.bind(this), 800);
  }
  this.localVideo_.src = this.miniVideo_.src;
  this.activate_(this.localVideo_);
  this.deactivate_(this.remoteVideo_);
  this.deactivate_(this.miniVideo_);
};
AppController.prototype.transitionToDone_ = function () {
  this.remoteVideo_.oncanplay = undefined;
  this.deactivate_(this.localVideo_);
  this.deactivate_(this.remoteVideo_);
  this.deactivate_(this.miniVideo_);
  this.hide_(this.hangupSvg_);
  this.activate_(this.rejoinDiv_);
  this.show_(this.rejoinDiv_);
  this.displayStatus_("");
};
AppController.prototype.onRejoinClick_ = function () {
  this.deactivate_(this.rejoinDiv_);
  this.hide_(this.rejoinDiv_);
  this.call_.restart();
};
AppController.prototype.onNewRoomClick_ = function () {
  this.deactivate_(this.rejoinDiv_);
  this.hide_(this.rejoinDiv_);
  this.showRoomSelection_();
};
AppController.prototype.onKeyPress_ = function (event) {
  switch (String.fromCharCode(event.charCode)) {
    case " ":
      ;
    case "m":
      if (this.call_) {
        this.call_.toggleAudioMute();
        this.muteAudioIconSet_.toggle();
      }
      return false;
    case "c":
      if (this.call_) {
        this.call_.toggleVideoMute();
        this.muteVideoIconSet_.toggle();
      }
      return false;
    case "f":
      this.toggleFullScreen_();
      return false;
    case "i":
      this.infoBox_.toggleInfoDiv();
      return false;
    case "q":
      this.hangup_();
      return false;
    default:
      return;
  }
};
AppController.prototype.pushCallNavigation_ = function (roomId, roomLink) {
  if (!isChromeApp()) {
    window.history.pushState({ "roomId": roomId, "roomLink": roomLink }, roomId, roomLink);
  }
};
AppController.prototype.displaySharingInfo_ = function (roomId, roomLink) {
  this.roomLinkHref_.href = roomLink;
  this.roomLinkHref_.text = roomLink;
  this.roomLink_ = roomLink;
  this.pushCallNavigation_(roomId, roomLink);
  this.activate_(this.sharingDiv_);
};
AppController.prototype.displayStatus_ = function (status) {
  if (status === "") {
    this.deactivate_(this.statusDiv_);
  } else {
    this.activate_(this.statusDiv_);
  }
  this.statusDiv_.innerHTML = status;
};
AppController.prototype.displayError_ = function (error) {
  trace(error);
  this.infoBox_.pushErrorMessage(error);
};
AppController.prototype.toggleAudioMute_ = function () {
  this.call_.toggleAudioMute();
  this.muteAudioIconSet_.toggle();
};
AppController.prototype.toggleVideoMute_ = function () {
  this.call_.toggleVideoMute();
  this.muteVideoIconSet_.toggle();
};
AppController.prototype.toggleFullScreen_ = function () {
  if (isFullScreen()) {
    trace("Exiting fullscreen.");
    document.cancelFullScreen();
  } else {
    trace("Entering fullscreen.");
    document.body.requestFullScreen();
  }
  this.fullscreenIconSet_.toggle();
};
AppController.prototype.hide_ = function (element) {
  element.classList.add("hidden");
};
AppController.prototype.show_ = function (element) {
  element.classList.remove("hidden");
};
AppController.prototype.activate_ = function (element) {
  element.classList.add("active");
};
AppController.prototype.deactivate_ = function (element) {
  element.classList.remove("active");
};
AppController.prototype.showIcons_ = function () {
  if (!this.icons_.classList.contains("active")) {
    this.activate_(this.icons_);
    setTimeout(function () {
      this.deactivate_(this.icons_);
    }.bind(this), 5E3);
  }
};
AppController.prototype.loadUrlParams_ = function () {
  var urlParams = queryStringToDictionary(window.location.search);
  this.loadingParams_.audioSendBitrate = urlParams["asbr"];
  this.loadingParams_.audioSendCodec = urlParams["asc"];
  this.loadingParams_.audioRecvBitrate = urlParams["arbr"];
  this.loadingParams_.audioRecvCodec = urlParams["arc"];
  this.loadingParams_.opusMaxPbr = urlParams["opusmaxpbr"];
  this.loadingParams_.opusFec = urlParams["opusfec"];
  this.loadingParams_.opusDtx = urlParams["opusdtx"];
  this.loadingParams_.opusStereo = urlParams["stereo"];
  this.loadingParams_.videoSendBitrate = urlParams["vsbr"];
  this.loadingParams_.videoSendInitialBitrate = urlParams["vsibr"];
  this.loadingParams_.videoSendCodec = urlParams["vsc"];
  this.loadingParams_.videoRecvBitrate = urlParams["vrbr"];
  this.loadingParams_.videoRecvCodec = urlParams["vrc"];
};
AppController.IconSet_ = function (iconSelector) {
  this.iconElement = document.querySelector(iconSelector);
};
AppController.IconSet_.prototype.toggle = function () {
  if (this.iconElement.classList.contains("on")) {
    this.iconElement.classList.remove("on");
  } else {
    this.iconElement.classList.add("on");
  }
};
var Call = function (params) {
  this.params_ = params;
  this.roomServer_ = params.roomServer || "";
  this.channel_ = new SignalingChannel(params.wssUrl, params.wssPostUrl);
  this.channel_.onmessage = this.onRecvSignalingChannelMessage_.bind(this);
  this.pcClient_ = null;
  this.localStream_ = null;
  this.startTime = null;
  this.oncallerstarted = null;
  this.onerror = null;
  this.oniceconnectionstatechange = null;
  this.onlocalstreamadded = null;
  this.onnewicecandidate = null;
  this.onremotehangup = null;
  this.onremotesdpset = null;
  this.onremotestreamadded = null;
  this.onsignalingstatechange = null;
  this.onstatusmessage = null;
  this.getMediaPromise_ = null;
  this.getTurnServersPromise_ = null;
  this.requestMediaAndTurnServers_();
};
Call.prototype.requestMediaAndTurnServers_ = function () {
  this.getMediaPromise_ = this.maybeGetMedia_();
  this.getTurnServersPromise_ = this.maybeGetTurnServers_();
};
Call.prototype.isInitiator = function () {
  return this.params_.isInitiator;
};
Call.prototype.start = function (roomId) {
  this.connectToRoom_(roomId);
  if (this.params_.isLoopback) {
    setupLoopback(this.params_.wssUrl, roomId);
  }
};
Call.prototype.queueCleanupMessages_ = function () {
  apprtc.windowPort.sendMessage({ action: Constants.QUEUEADD_ACTION, queueMessage: { action: Constants.XHR_ACTION, method: "POST", url: this.getLeaveUrl_(), body: null } });
  apprtc.windowPort.sendMessage({ action: Constants.QUEUEADD_ACTION, queueMessage: { action: Constants.WS_ACTION, wsAction: Constants.WS_SEND_ACTION, data: JSON.stringify({ cmd: "send", msg: JSON.stringify({ type: "bye" }) }) } });
  apprtc.windowPort.sendMessage({ action: Constants.QUEUEADD_ACTION, queueMessage: { action: Constants.XHR_ACTION, method: "DELETE", url: this.channel_.getWssPostUrl(), body: null } });
};
Call.prototype.clearCleanupQueue_ = function () {
  apprtc.windowPort.sendMessage({ action: Constants.QUEUECLEAR_ACTION });
};
Call.prototype.restart = function () {
  this.requestMediaAndTurnServers_();
  this.start(this.params_.previousRoomId);
};

Call.prototype.onWinRTAppSuspending = function () {
  this.hangup(false);
}

Call.prototype.hangup = function (async) {
  this.startTime = null;
  if (isChromeApp()) {
    this.clearCleanupQueue_();
  }
  if (this.localStream_) {

    // For winrt, need to explicitly remove tracks to release local audio/video resource
    if (navigator.releaseUserMedia) {

      navigator.releaseUserMedia(this.localStream_);

    }
    this.localStream_.stop();
    this.localStream_ = null;
  }
  if (!this.params_.roomId) {
    return;
  }
  if (this.pcClient_) {
    this.pcClient_.close();
    this.pcClient_ = null;
  }
  var steps = [];
  steps.push({
    step: function () {
      var path = this.getLeaveUrl_();
      return sendUrlRequest("POST", path, async);
    }.bind(this), errorString: "Error sending /leave:"
  });
  steps.push({
    step: function () {
      this.channel_.send(JSON.stringify({ type: "bye" }));
    }.bind(this), errorString: "Error sending bye:"
  });
  steps.push({
    step: function () {
      return this.channel_.close(async);
    }.bind(this), errorString: "Error closing signaling channel:"
  });
  steps.push({
    step: function () {
      this.params_.previousRoomId = this.params_.roomId;
      this.params_.roomId = null;
      this.params_.clientId = null;
    }.bind(this), errorString: "Error setting params:"
  });
  if (async) {
    var errorHandler = function (errorString, error) {
      trace(errorString + " " + error.message);
    };
    var promise = Promise.resolve();
    for (var i = 0; i < steps.length; ++i) {
      promise = promise.then(steps[i].step).catch(errorHandler.bind(this, steps[i].errorString));
    }
    return promise;
  } else {
    var executeStep = function (executor, errorString) {
      try {
        executor();
      } catch (ex) {
        trace(errorString + " " + ex);
      }
    };
    for (var j = 0; j < steps.length; ++j) {
      executeStep(steps[j].step, steps[j].errorString);
    }
    if (this.params_.roomId !== null || this.params_.clientId !== null) {
      trace("ERROR: sync cleanup tasks did not complete successfully.");
    } else {
      trace("Cleanup completed.");
    }
    return Promise.resolve();
  }
};
Call.prototype.getLeaveUrl_ = function () {
  return this.roomServer_ + "/leave/" + this.params_.roomId + "/" + this.params_.clientId;
};
Call.prototype.onRemoteHangup = function () {
  this.startTime = null;
  this.params_.isInitiator = true;
  if (this.pcClient_) {
    this.pcClient_.close();
    this.pcClient_ = null;
  }
  this.startSignaling_();
};
Call.prototype.getPeerConnectionStates = function () {
  if (!this.pcClient_) {
    return null;
  }
  return this.pcClient_.getPeerConnectionStates();
};
Call.prototype.getPeerConnectionStats = function (callback) {
  if (!this.pcClient_) {
    return;
  }
  this.pcClient_.getPeerConnectionStats(callback);
};
Call.prototype.toggleVideoMute = function () {
  var videoTracks = this.localStream_.getVideoTracks();
  if (videoTracks.length === 0) {
    trace("No local video available.");
    return;
  }
  trace("Toggling video mute state.");
  for (var i = 0; i < videoTracks.length; ++i) {
    videoTracks[i].enabled = !videoTracks[i].enabled;
  }
  trace("Video " + (videoTracks[0].enabled ? "unmuted." : "muted."));
};
Call.prototype.toggleAudioMute = function () {
  var audioTracks = this.localStream_.getAudioTracks();
  if (audioTracks.length === 0) {
    trace("No local audio available.");
    return;
  }
  trace("Toggling audio mute state.");
  for (var i = 0; i < audioTracks.length; ++i) {
    audioTracks[i].enabled = !audioTracks[i].enabled;
  }
  trace("Audio " + (audioTracks[0].enabled ? "unmuted." : "muted."));
};
Call.prototype.connectToRoom_ = function (roomId) {
  this.params_.roomId = roomId;
  var channelPromise = this.channel_.open().catch(function (error) {
    this.onError_("WebSocket open error: " + error.message);
    return Promise.reject(error);
  }.bind(this));
  var joinPromise = this.joinRoom_().then(function (roomParams) {
    this.params_.clientId = roomParams.client_id;
    this.params_.roomId = roomParams.room_id;
    this.params_.roomLink = roomParams.room_link;
    this.params_.isInitiator = roomParams.is_initiator === "true";
    this.params_.messages = roomParams.messages;
  }.bind(this)).catch(function (error) {
    this.onError_("Room server join error: " + error.message);
    return Promise.reject(error);
  }.bind(this));
  Promise.all([channelPromise, joinPromise]).then(function () {
    this.channel_.register(this.params_.roomId, this.params_.clientId);
    Promise.all([this.getTurnServersPromise_, this.getMediaPromise_]).then(function () {
      this.startSignaling_();
      if (isChromeApp()) {
        this.queueCleanupMessages_();
      }
    }.bind(this)).catch(function (error) {
      this.onError_("Failed to start signaling: " + error.message);
    }.bind(this));
  }.bind(this)).catch(function (error) {
    this.onError_("WebSocket register error: " + error.message);
  }.bind(this));
};
Call.prototype.maybeGetMedia_ = function () {
  var needStream = this.params_.mediaConstraints.audio !== false || this.params_.mediaConstraints.video !== false;
  var mediaPromise = null;
  if (needStream) {
    var mediaConstraints = this.params_.mediaConstraints;
    mediaPromise = requestUserMedia(mediaConstraints).then(function (stream) {
      trace("Got access to local media with mediaConstraints:\n" + "  '" + JSON.stringify(mediaConstraints) + "'");
      this.onUserMediaSuccess_(stream);
    }.bind(this)).catch(function (error) {
      this.onError_("Error getting user media: " + error.message);
      this.onUserMediaError_(error);
    }.bind(this));
  } else {
    mediaPromise = Promise.resolve();
  }
  return mediaPromise;
};
Call.prototype.maybeGetTurnServers_ = function () {
  var shouldRequestTurnServers = this.params_.turnRequestUrl && this.params_.turnRequestUrl.length > 0;
  var turnPromise = null;
  if (shouldRequestTurnServers) {
    var requestUrl = this.params_.turnRequestUrl;
    turnPromise = requestTurnServers(requestUrl, this.params_.turnTransports).then(function (turnServers) {
      var iceServers = this.params_.peerConnectionConfig.iceServers;
      this.params_.peerConnectionConfig.iceServers = iceServers.concat(turnServers);
    }.bind(this)).catch(function (error) {
      if (this.onstatusmessage) {
        var subject = encodeURIComponent("AppRTC demo TURN server not working");
        this.onstatusmessage("No TURN server; unlikely that media will traverse networks. " + "If this persists please " + '<a href="mailto:discuss-webrtc@googlegroups.com?' + "subject=" + subject + '">' + "report it to discuss-webrtc@googlegroups.com</a>.");
      }
      trace(error.message);
    }.bind(this));
  } else {
    turnPromise = Promise.resolve();
  }
  return turnPromise;
};
Call.prototype.onUserMediaSuccess_ = function (stream) {
  this.localStream_ = stream;
  if (this.onlocalstreamadded) {
    this.onlocalstreamadded(stream);
  }
};
Call.prototype.onUserMediaError_ = function (error) {
  var errorMessage = "Failed to get access to local media. Error name was " + error.name + ". Continuing without sending a stream.";
  this.onError_("getUserMedia error: " + errorMessage);
  alert(errorMessage);
};
Call.prototype.maybeCreatePcClient_ = function () {
  if (this.pcClient_) {
    return;
  }
  try {
    this.pcClient_ = new PeerConnectionClient(this.params_, this.startTime);
    console.log("create faile");
    this.pcClient_.onsignalingmessage = this.sendSignalingMessage_.bind(this);
    this.pcClient_.onremotehangup = this.onremotehangup;
    this.pcClient_.onremotesdpset = this.onremotesdpset;
    this.pcClient_.onremotestreamadded = this.onremotestreamadded;
    this.pcClient_.onsignalingstatechange = this.onsignalingstatechange;
    this.pcClient_.oniceconnectionstatechange = this.oniceconnectionstatechange;
    this.pcClient_.onnewicecandidate = this.onnewicecandidate;
    this.pcClient_.onerror = this.onerror;
    trace("Created PeerConnectionClient");
  } catch (e) {
    this.onError_("Create PeerConnection exception: " + e.message);
    alert("Cannot create RTCPeerConnection; " + "WebRTC is not supported by this browser.");
    return;
  }
};
Call.prototype.startSignaling_ = function () {
  trace("Starting signaling.");
  if (this.isInitiator() && this.oncallerstarted) {
    this.oncallerstarted(this.params_.roomId, this.params_.roomLink);
  }
  this.startTime = window.performance.now();
  this.maybeCreatePcClient_();
  if (this.localStream_) {
    trace("Adding local stream.");
    this.pcClient_.addStream(this.localStream_);
  }
  if (this.params_.isInitiator) {
    this.pcClient_.startAsCaller(this.params_.offerConstraints);
  } else {
    this.pcClient_.startAsCallee(this.params_.messages);
  }
};
Call.prototype.joinRoom_ = function () {
  return new Promise(function (resolve, reject) {
    if (!this.params_.roomId) {
      reject(Error("Missing room id."));
    }
    var path = this.roomServer_ + "/join/" + this.params_.roomId + window.location.search;
    sendAsyncUrlRequest("POST", path).then(function (response) {
      var responseObj = parseJSON(response);
      if (!responseObj) {
        reject(Error("Error parsing response JSON."));
        return;
      }
      if (responseObj.result !== "SUCCESS") {
        reject(Error("Registration error: " + responseObj.result));
        return;
      }
      trace("Joined the room.");
      resolve(responseObj.params);
    }.bind(this)).catch(function (error) {
      reject(Error("Failed to join the room: " + error.message));
      return;
    }.bind(this));
  }.bind(this));
};
Call.prototype.onRecvSignalingChannelMessage_ = function (msg) {
  this.maybeCreatePcClient_();
  this.pcClient_.receiveSignalingMessage(msg);
};
Call.prototype.sendSignalingMessage_ = function (message) {
  var msgString = JSON.stringify(message);
  if (this.params_.isInitiator) {
    var path = this.roomServer_ + "/message/" + this.params_.roomId + "/" + this.params_.clientId + window.location.search;
    var xhr = new XMLHttpRequest;
    xhr.open("POST", path, true);
    xhr.send(msgString);
    trace("C->GAE: " + msgString);
  } else {
    this.channel_.send(msgString);
  }
};
Call.prototype.onError_ = function (message) {
  if (this.onerror) {
    this.onerror(message);
  }
};
var Constants = { WS_ACTION: "ws", XHR_ACTION: "xhr", QUEUEADD_ACTION: "addToQueue", QUEUECLEAR_ACTION: "clearQueue", EVENT_ACTION: "event", WS_CREATE_ACTION: "create", WS_EVENT_ONERROR: "onerror", WS_EVENT_ONMESSAGE: "onmessage", WS_EVENT_ONOPEN: "onopen", WS_EVENT_ONCLOSE: "onclose", WS_EVENT_SENDERROR: "onsenderror", WS_SEND_ACTION: "send", WS_CLOSE_ACTION: "close" };
var InfoBox = function (infoDiv, remoteVideo, call, versionInfo) {
  this.infoDiv_ = infoDiv;
  this.remoteVideo_ = remoteVideo;
  this.call_ = call;
  this.versionInfo_ = versionInfo;
  this.errorMessages_ = [];
  this.startTime_ = null;
  this.connectTime_ = null;
  this.stats_ = null;
  this.prevStats_ = null;
  this.getStatsTimer_ = null;
  this.iceCandidateTypes_ = { Local: {}, Remote: {} };
};
InfoBox.prototype.recordIceCandidateTypes = function (location, candidate) {
  var type = iceCandidateType(candidate);
  var types = this.iceCandidateTypes_[location];
  if (!types[type]) {
    types[type] = 1;
  } else {
    ++types[type];
  }
  this.updateInfoDiv();
};
InfoBox.prototype.pushErrorMessage = function (msg) {
  this.errorMessages_.push(msg);
  this.updateInfoDiv();
  this.showInfoDiv();
};
InfoBox.prototype.setSetupTimes = function (startTime, connectTime) {
  this.startTime_ = startTime;
  this.connectTime_ = connectTime;
};
InfoBox.prototype.showInfoDiv = function () {
  this.getStatsTimer_ = setInterval(this.refreshStats_.bind(this), 1E3);
  this.refreshStats_();
  this.infoDiv_.classList.add("active");
};
InfoBox.prototype.toggleInfoDiv = function () {
  if (this.infoDiv_.classList.contains("active")) {
    clearInterval(this.getStatsTimer_);
    this.infoDiv_.classList.remove("active");
  } else {
    this.showInfoDiv();
  }
};
InfoBox.prototype.refreshStats_ = function () {
  this.call_.getPeerConnectionStats(function (response) {
    this.prevStats_ = this.stats_;
    this.stats_ = response.result();
    this.updateInfoDiv();
  }.bind(this));
};
InfoBox.prototype.updateInfoDiv = function () {
  var contents = '<pre id="info-box-stats" style="line-height: initial">';
  if (this.stats_) {
    var states = this.call_.getPeerConnectionStates();
    if (!states) {
      return;
    }
    contents += this.buildLine_("States");
    contents += this.buildLine_("Signaling", states.signalingState);
    contents += this.buildLine_("Gathering", states.iceGatheringState);
    contents += this.buildLine_("Connection", states.iceConnectionState);
    for (var endpoint in this.iceCandidateTypes_) {
      var types = [];
      for (var type in this.iceCandidateTypes_[endpoint]) {
        types.push(type + ":" + this.iceCandidateTypes_[endpoint][type]);
      }
      contents += this.buildLine_(endpoint, types.join(" "));
    }
    var activeCandPair = getStatsReport(this.stats_, "googCandidatePair", "googActiveConnection", "true");
    var localAddr;
    var remoteAddr;
    var localAddrType;
    var remoteAddrType;
    if (activeCandPair) {
      localAddr = activeCandPair.stat("googLocalAddress");
      remoteAddr = activeCandPair.stat("googRemoteAddress");
      localAddrType = activeCandPair.stat("googLocalCandidateType");
      remoteAddrType = activeCandPair.stat("googRemoteCandidateType");
    }
    if (localAddr && remoteAddr) {
      contents += this.buildLine_("LocalAddr", localAddr + " (" + localAddrType + ")");
      contents += this.buildLine_("RemoteAddr", remoteAddr + " (" + remoteAddrType + ")");
    }
    contents += this.buildLine_();
    contents += this.buildStatsSection_();
  }
  if (this.errorMessages_.length) {
    this.infoDiv_.classList.add("warning");
    for (var i = 0; i !== this.errorMessages_.length; ++i) {
      contents += this.errorMessages_[i] + "\n";
    }
  } else {
    this.infoDiv_.classList.remove("warning");
  }
  if (this.versionInfo_) {
    contents += this.buildLine_();
    contents += this.buildLine_("Version");
    for (var key in this.versionInfo_) {
      contents += this.buildLine_(key, this.versionInfo_[key]);
    }
  }
  contents += "</pre>";
  if (this.infoDiv_.innerHTML !== contents) {
    this.infoDiv_.innerHTML = contents;
  }
};
InfoBox.prototype.buildStatsSection_ = function () {
  var contents = this.buildLine_("Stats");
  var rtt = extractStatAsInt(this.stats_, "ssrc", "googRtt");
  var captureStart = extractStatAsInt(this.stats_, "ssrc", "googCaptureStartNtpTimeMs");
  var e2eDelay = computeE2EDelay(captureStart, this.remoteVideo_.currentTime);
  if (this.endTime_ !== null) {
    contents += this.buildLine_("Call time", InfoBox.formatInterval_(window.performance.now() - this.connectTime_));
    contents += this.buildLine_("Setup time", InfoBox.formatMsec_(this.connectTime_ - this.startTime_));
  }
  if (rtt !== null) {
    contents += this.buildLine_("RTT", InfoBox.formatMsec_(rtt));
  }
  if (e2eDelay !== null) {
    contents += this.buildLine_("End to end", InfoBox.formatMsec_(e2eDelay));
  }
  var txAudio = getStatsReport(this.stats_, "ssrc", "audioInputLevel");
  var rxAudio = getStatsReport(this.stats_, "ssrc", "audioOutputLevel");
  var txVideo = getStatsReport(this.stats_, "ssrc", "googFirsReceived");
  var rxVideo = getStatsReport(this.stats_, "ssrc", "googFirsSent");
  var txPrevAudio = getStatsReport(this.prevStats_, "ssrc", "audioInputLevel");
  var rxPrevAudio = getStatsReport(this.prevStats_, "ssrc", "audioOutputLevel");
  var txPrevVideo = getStatsReport(this.prevStats_, "ssrc", "googFirsReceived");
  var rxPrevVideo = getStatsReport(this.prevStats_, "ssrc", "googFirsSent");
  var txAudioCodec;
  var txAudioBitrate;
  var txAudioPacketRate;
  var rxAudioCodec;
  var rxAudioBitrate;
  var rxAudioPacketRate;
  var txVideoHeight;
  var txVideoFps;
  var txVideoCodec;
  var txVideoBitrate;
  var txVideoPacketRate;
  var rxVideoHeight;
  var rxVideoFps;
  var rxVideoCodec;
  var rxVideoBitrate;
  var rxVideoPacketRate;
  if (txAudio) {
    txAudioCodec = txAudio.stat("googCodecName");
    txAudioBitrate = computeBitrate(txAudio, txPrevAudio, "bytesSent");
    txAudioPacketRate = computeRate(txAudio, txPrevAudio, "packetsSent");
    contents += this.buildLine_("Audio Tx", txAudioCodec + ", " + InfoBox.formatBitrate_(txAudioBitrate) + ", " + InfoBox.formatPacketRate_(txAudioPacketRate));
  }
  if (rxAudio) {
    rxAudioCodec = rxAudio.stat("googCodecName");
    rxAudioBitrate = computeBitrate(rxAudio, rxPrevAudio, "bytesReceived");
    rxAudioPacketRate = computeRate(rxAudio, rxPrevAudio, "packetsReceived");
    contents += this.buildLine_("Audio Rx", rxAudioCodec + ", " + InfoBox.formatBitrate_(rxAudioBitrate) + ", " + InfoBox.formatPacketRate_(rxAudioPacketRate));
  }
  if (txVideo) {
    txVideoCodec = txVideo.stat("googCodecName");
    txVideoHeight = txVideo.stat("googFrameHeightSent");
    txVideoFps = txVideo.stat("googFrameRateSent");
    txVideoBitrate = computeBitrate(txVideo, txPrevVideo, "bytesSent");
    txVideoPacketRate = computeRate(txVideo, txPrevVideo, "packetsSent");
    contents += this.buildLine_("Video Tx", txVideoCodec + ", " + txVideoHeight.toString() + "p" + txVideoFps.toString() + ", " + InfoBox.formatBitrate_(txVideoBitrate) + ", " + InfoBox.formatPacketRate_(txVideoPacketRate));
  }
  if (rxVideo) {
    rxVideoCodec = "TODO";
    rxVideoHeight = this.remoteVideo_.videoHeight;
    rxVideoFps = rxVideo.stat("googFrameRateDecoded");
    rxVideoBitrate = computeBitrate(rxVideo, rxPrevVideo, "bytesReceived");
    rxVideoPacketRate = computeRate(rxVideo, rxPrevVideo, "packetsReceived");
    contents += this.buildLine_("Video Rx", rxVideoCodec + ", " + rxVideoHeight.toString() + "p" + rxVideoFps.toString() + ", " + InfoBox.formatBitrate_(rxVideoBitrate) + ", " + InfoBox.formatPacketRate_(rxVideoPacketRate));
  }
  return contents;
};
InfoBox.prototype.buildLine_ = function (label, value) {
  var columnWidth = 12;
  var line = "";
  if (label) {
    line += label + ":";
    while (line.length < columnWidth) {
      line += " ";
    }
    if (value) {
      line += value;
    }
  }
  line += "\n";
  return line;
};
InfoBox.formatInterval_ = function (value) {
  var result = "";
  var seconds = Math.floor(value / 1E3);
  var minutes = Math.floor(seconds / 60);
  var hours = Math.floor(minutes / 60);
  var formatTwoDigit = function (twodigit) {
    return (twodigit < 10 ? "0" : "") + twodigit.toString();
  };
  if (hours > 0) {
    result += formatTwoDigit(hours) + ":";
  }
  result += formatTwoDigit(minutes - hours * 60) + ":";
  result += formatTwoDigit(seconds - minutes * 60);
  return result;
};
InfoBox.formatMsec_ = function (value) {
  return value.toFixed(0).toString() + " ms";
};
InfoBox.formatBitrate_ = function (value) {
  if (!value) {
    return "- bps";
  }
  var suffix;
  if (value < 1E3) {
    suffix = "bps";
  } else {
    if (value < 1E6) {
      suffix = "kbps";
      value /= 1E3;
    } else {
      suffix = "Mbps";
      value /= 1E6;
    }
  }
  var str = value.toPrecision(3) + " " + suffix;
  return str;
};
InfoBox.formatPacketRate_ = function (value) {
  if (!value) {
    return "- pps";
  }
  return value.toPrecision(3) + " " + "pps";
};
var PeerConnectionClient = function (params, startTime) {
  this.params_ = params;
  this.startTime_ = startTime;
  trace("Creating RTCPeerConnnection with:\n" + "  config: '" + JSON.stringify(params.peerConnectionConfig) + "';\n" + "  constraints: '" + JSON.stringify(params.peerConnectionConstraints) + "'.");
  this.pc_ = new RTCPeerConnection(params.peerConnectionConfig, params.peerConnectionConstraints);
  this.pc_.onicecandidate = this.onIceCandidate_.bind(this);
  this.pc_.onaddstream = this.onRemoteStreamAdded_.bind(this);
  this.pc_.onremovestream = trace.bind(null, "Remote stream removed.");
  this.pc_.onsignalingstatechange = this.onSignalingStateChanged_.bind(this);
  this.pc_.oniceconnectionstatechange = this.onIceConnectionStateChanged_.bind(this);
  this.hasRemoteSdp_ = false;
  this.messageQueue_ = [];
  this.isInitiator_ = false;
  this.started_ = false;
  this.onerror = null;
  this.oniceconnectionstatechange = null;
  this.onnewicecandidate = null;
  this.onremotehangup = null;
  this.onremotesdpset = null;
  this.onremotestreamadded = null;
  this.onsignalingmessage = null;
  this.onsignalingstatechange = null;
};
PeerConnectionClient.DEFAULT_SDP_CONSTRAINTS_ = { "mandatory": { "OfferToReceiveAudio": true, "OfferToReceiveVideo": true }, "optional": [{ "VoiceActivityDetection": false }] };
PeerConnectionClient.prototype.addStream = function (stream) {
  if (!this.pc_) {
    return;
  }
  this.pc_.addStream(stream);
};
PeerConnectionClient.prototype.startAsCaller = function (offerConstraints) {
  if (!this.pc_) {
    return false;
  }
  if (this.started_) {
    return false;
  }
  this.isInitiator_ = true;
  this.started_ = true;
  var constraints = mergeConstraints(offerConstraints, PeerConnectionClient.DEFAULT_SDP_CONSTRAINTS_);
  trace("Sending offer to peer, with constraints: \n'" + JSON.stringify(constraints) + "'.");
  this.pc_.createOffer(constraints).then(this.setLocalSdpAndNotify_.bind(this)).catch(this.onError_.bind(this, "createOffer"));

  return true;
};
PeerConnectionClient.prototype.startAsCallee = function (initialMessages) {
  if (!this.pc_) {
    return false;
  }
  if (this.started_) {
    return false;
  }
  this.isInitiator_ = false;
  this.started_ = true;
  if (initialMessages && initialMessages.length > 0) {
    for (var i = 0, len = initialMessages.length; i < len; i++) {
      this.receiveSignalingMessage(initialMessages[i]);
    }
    return true;
  }
  if (this.messageQueue_.length > 0) {
    this.drainMessageQueue_();
  }
  return true;
};
PeerConnectionClient.prototype.receiveSignalingMessage = function (message) {
  var messageObj = parseJSON(message);
  if (!messageObj) {
    return;
  }
  if (this.isInitiator_ && messageObj.type === "answer" || !this.isInitiator_ && messageObj.type === "offer") {
    this.hasRemoteSdp_ = true;
    this.messageQueue_.unshift(messageObj);
  } else {
    if (messageObj.type === "candidate") {
      this.messageQueue_.push(messageObj);
    } else {
      if (messageObj.type === "bye") {
        if (this.onremotehangup) {
          this.onremotehangup();
        }
      }
    }
  }
  this.drainMessageQueue_();
};
PeerConnectionClient.prototype.close = function () {
  if (!this.pc_) {
    return;
  }
  this.pc_.close();
  this.pc_ = null;
};
PeerConnectionClient.prototype.getPeerConnectionStates = function () {
  if (!this.pc_) {
    return null;
  }
  return { "signalingState": this.pc_.signalingState, "iceGatheringState": this.pc_.iceGatheringState, "iceConnectionState": this.pc_.iceConnectionState };
};
PeerConnectionClient.prototype.getPeerConnectionStats = function (callback) {
  if (!this.pc_) {
    return;
  }
  this.pc_.getStats(callback);
};
PeerConnectionClient.prototype.doAnswer_ = function () {
  trace("Sending answer to peer.");
  var p;
  if (webrtcDetectedBrowser === "firefox") {
    p = this.pc_.createAnswer();
  } else {
    p = this.pc_.createAnswer(PeerConnectionClient.DEFAULT_SDP_CONSTRAINTS_);
  }
  p.then(this.setLocalSdpAndNotify_.bind(this)).catch(this.onError_.bind(this, "createAnswer"));
};
PeerConnectionClient.prototype.setLocalSdpAndNotify_ = function (sessionDescription) {
  sessionDescription.sdp = maybePreferAudioReceiveCodec(sessionDescription.sdp, this.params_);
  sessionDescription.sdp = maybePreferVideoReceiveCodec(sessionDescription.sdp, this.params_);
  sessionDescription.sdp = maybeSetAudioReceiveBitRate(sessionDescription.sdp, this.params_);
  sessionDescription.sdp = maybeSetVideoReceiveBitRate(sessionDescription.sdp, this.params_);
  this.pc_.setLocalDescription(sessionDescription).then(trace.bind(null, "Set session description success.")).catch(this.onError_.bind(this, "setLocalDescription"));
  if (this.onsignalingmessage) {
    this.onsignalingmessage({ sdp: sessionDescription.sdp, type: sessionDescription.type });
  }
};
PeerConnectionClient.prototype.setRemoteSdp_ = function (message) {
  message.sdp = maybeSetOpusOptions(message.sdp, this.params_);
  message.sdp = maybePreferAudioSendCodec(message.sdp, this.params_);
  message.sdp = maybePreferVideoSendCodec(message.sdp, this.params_);
  message.sdp = maybeSetAudioSendBitRate(message.sdp, this.params_);
  message.sdp = maybeSetVideoSendBitRate(message.sdp, this.params_);
  message.sdp = maybeSetVideoSendInitialBitRate(message.sdp, this.params_);
  this.pc_.setRemoteDescription(new RTCSessionDescription(message)).then(this.onSetRemoteDescriptionSuccess_.bind(this)).catch(this.onError_.bind(this, "setRemoteDescription"));
};
PeerConnectionClient.prototype.onSetRemoteDescriptionSuccess_ = function () {
  trace("Set remote session description success.");
  var remoteStreams = this.pc_.getRemoteStreams();
  if (this.onremotesdpset) {
    this.onremotesdpset(remoteStreams.length > 0 && remoteStreams[0].getVideoTracks().length > 0);
  }
};
PeerConnectionClient.prototype.processSignalingMessage_ = function (message) {
  if (message.type === "offer" && !this.isInitiator_) {
    if (this.pc_.signalingState !== "stable") {
      trace("ERROR: remote offer received in unexpected state: " + this.pc_.signalingState);
      return;
    }
    this.setRemoteSdp_(message);
    this.doAnswer_();
  } else {
    if (message.type === "answer" && this.isInitiator_) {
      if (this.pc_.signalingState !== "have-local-offer") {
        trace("ERROR: remote answer received in unexpected state: " + this.pc_.signalingState);
        return;
      }
      this.setRemoteSdp_(message);
    } else {
      if (message.type === "candidate") {
        var candidate = new RTCIceCandidate({ sdpMLineIndex: message.label, candidate: message.candidate });
        this.recordIceCandidate_("Remote", candidate);
        this.pc_.addIceCandidate(candidate).then(trace.bind(null, "Remote candidate added successfully.")).catch(this.onError_.bind(this, "addIceCandidate"));
      } else {
        trace("WARNING: unexpected message: " + JSON.stringify(message));
      }
    }
  }
};
PeerConnectionClient.prototype.drainMessageQueue_ = function () {
  if (!this.pc_ || !this.started_ || !this.hasRemoteSdp_) {
    return;
  }
  for (var i = 0, len = this.messageQueue_.length; i < len; i++) {
    this.processSignalingMessage_(this.messageQueue_[i]);
  }
  this.messageQueue_ = [];
};
PeerConnectionClient.prototype.onIceCandidate_ = function (event) {
  if (event.candidate) {
    if (this.filterIceCandidate_(event.candidate)) {
      var message = { type: "candidate", label: event.candidate.sdpMLineIndex, id: event.candidate.sdpMid, candidate: event.candidate.candidate };
      if (this.onsignalingmessage) {
        this.onsignalingmessage(message);
      }
      this.recordIceCandidate_("Local", event.candidate);
    }
  } else {
    trace("End of candidates.");
  }
};
PeerConnectionClient.prototype.onSignalingStateChanged_ = function () {
  if (!this.pc_) {
    return;
  }
  trace("Signaling state changed to: " + this.pc_.signalingState);
  if (this.onsignalingstatechange) {
    this.onsignalingstatechange();
  }
};
PeerConnectionClient.prototype.onIceConnectionStateChanged_ = function () {
  if (!this.pc_) {
    return;
  }
  trace("ICE connection state changed to: " + this.pc_.iceConnectionState);
  if (this.pc_.iceConnectionState === "completed") {
    trace("ICE complete time: " + (window.performance.now() - this.startTime_).toFixed(0) + "ms.");
  }
  if (this.oniceconnectionstatechange) {
    this.oniceconnectionstatechange();
  }
};
PeerConnectionClient.prototype.filterIceCandidate_ = function (candidateObj) {
  var candidateStr = candidateObj.candidate;
  if (candidateStr.indexOf("tcp") !== -1) {
    return false;
  }
  if (this.params_.peerConnectionConfig.iceTransports === "relay" && iceCandidateType(candidateStr) !== "relay") {
    return false;
  }
  return true;
};
PeerConnectionClient.prototype.recordIceCandidate_ = function (location, candidateObj) {
  if (this.onnewicecandidate) {
    this.onnewicecandidate(location, candidateObj.candidate);
  }
};
PeerConnectionClient.prototype.onRemoteStreamAdded_ = function (event) {
  if (this.onremotestreamadded) {
    this.onremotestreamadded(event.stream);
  }
};
PeerConnectionClient.prototype.onError_ = function (tag, error) {
  if (this.onerror) {
    this.onerror(tag + ": " + error.toString());
  }
};
var RemoteWebSocket = function (wssUrl, wssPostUrl) {
  this.wssUrl_ = wssUrl;
  apprtc.windowPort.addMessageListener(this.handleMessage_.bind(this));
  this.sendMessage_({ action: Constants.WS_ACTION, wsAction: Constants.WS_CREATE_ACTION, wssUrl: wssUrl, wssPostUrl: wssPostUrl });
  this.readyState = WebSocket.CONNECTING;
};
RemoteWebSocket.prototype.sendMessage_ = function (message) {
  apprtc.windowPort.sendMessage(message);
};
RemoteWebSocket.prototype.send = function (data) {
  if (this.readyState !== WebSocket.OPEN) {
    throw "Web socket is not in OPEN state: " + this.readyState;
  }
  this.sendMessage_({ action: Constants.WS_ACTION, wsAction: Constants.WS_SEND_ACTION, data: data });
};
RemoteWebSocket.prototype.close = function () {
  if (this.readyState === WebSocket.CLOSING || this.readyState === WebSocket.CLOSED) {
    return;
  }
  this.readyState = WebSocket.CLOSING;
  this.sendMessage_({ action: Constants.WS_ACTION, wsAction: Constants.WS_CLOSE_ACTION });
};
RemoteWebSocket.prototype.handleMessage_ = function (message) {
  if (message.action === Constants.WS_ACTION && message.wsAction === Constants.EVENT_ACTION) {
    if (message.wsEvent === Constants.WS_EVENT_ONOPEN) {
      this.readyState = WebSocket.OPEN;
      if (this.onopen) {
        this.onopen();
      }
    } else {
      if (message.wsEvent === Constants.WS_EVENT_ONCLOSE) {
        this.readyState = WebSocket.CLOSED;
        if (this.onclose) {
          this.onclose(message.data);
        }
      } else {
        if (message.wsEvent === Constants.WS_EVENT_ONERROR) {
          if (this.onerror) {
            this.onerror(message.data);
          }
        } else {
          if (message.wsEvent === Constants.WS_EVENT_ONMESSAGE) {
            if (this.onmessage) {
              this.onmessage(message.data);
            }
          } else {
            if (message.wsEvent === Constants.WS_EVENT_SENDERROR) {
              if (this.onsenderror) {
                this.onsenderror(message.data);
              }
              trace("ERROR: web socket send failed: " + message.data);
            }
          }
        }
      }
    }
  }
};
var RoomSelection = function (roomSelectionDiv, uiConstants, recentRoomsKey, setupCompletedCallback) {
  this.roomSelectionDiv_ = roomSelectionDiv;
  this.setupCompletedCallback_ = setupCompletedCallback;
  this.roomIdInput_ = this.roomSelectionDiv_.querySelector(uiConstants.roomSelectionInput);
  this.roomIdInputLabel_ = this.roomSelectionDiv_.querySelector(uiConstants.roomSelectionInputLabel);
  this.roomJoinButton_ = this.roomSelectionDiv_.querySelector(uiConstants.roomSelectionJoinButton);
  this.roomRandomButton_ = this.roomSelectionDiv_.querySelector(uiConstants.roomSelectionRandomButton);
  this.settingButton_ = this.roomSelectionDiv_.querySelector(uiConstants.settingButton);
  this.roomRecentList_ = this.roomSelectionDiv_.querySelector(uiConstants.roomSelectionRecentList);
  this.roomIdInput_.value = randomString(9);
  this.onRoomIdInput_();
  this.roomIdInputListener_ = this.onRoomIdInput_.bind(this);
  this.roomIdInput_.addEventListener("input", this.roomIdInputListener_, false);
  this.roomIdKeyupListener_ = this.onRoomIdKeyPress_.bind(this);
  this.roomIdInput_.addEventListener("keyup", this.roomIdKeyupListener_, false);
  this.roomRandomButtonListener_ = this.onRandomButton_.bind(this);
  this.roomRandomButton_.addEventListener("click", this.roomRandomButtonListener_, false);
  this.roomJoinButtonListener_ = this.onJoinButton_.bind(this);
  this.settingButtonListener_ = this.onSettingButton_.bind(this);
  this.settingButton_.addEventListener("click", this.settingButtonListener_, false);
  this.roomJoinButton_.addEventListener("click", this.roomJoinButtonListener_, false);
  this.onRoomSelected = null;
  this.recentlyUsedList_ = new RoomSelection.RecentlyUsedList(recentRoomsKey);
  this.startBuildingRecentRoomList_();
};
RoomSelection.matchRandomRoomPattern = function (input) {
  return input.match(/^\d{9}$/) !== null;
};
RoomSelection.prototype.removeEventListeners = function () {
  this.roomIdInput_.removeEventListener("input", this.roomIdInputListener_);
  this.roomIdInput_.removeEventListener("keyup", this.roomIdKeyupListener_);
  this.roomRandomButton_.removeEventListener("click", this.roomRandomButtonListener_);
  this.roomJoinButton_.removeEventListener("click", this.roomJoinButtonListener_);
};
RoomSelection.prototype.startBuildingRecentRoomList_ = function () {
  this.recentlyUsedList_.getRecentRooms().then(function (recentRooms) {
    this.buildRecentRoomList_(recentRooms);
    if (this.setupCompletedCallback_) {
      this.setupCompletedCallback_();
    }
  }.bind(this)).catch(function (error) {
    trace("Error building recent rooms list: " + error.message);
  }.bind(this));
};
RoomSelection.prototype.buildRecentRoomList_ = function (recentRooms) {
  var lastChild = this.roomRecentList_.lastChild;
  while (lastChild) {
    this.roomRecentList_.removeChild(lastChild);
    lastChild = this.roomRecentList_.lastChild;
  }
  for (var i = 0; i < recentRooms.length; ++i) {
    var li = document.createElement("li");
    var href = document.createElement("a");
    var linkText = document.createTextNode(recentRooms[i]);
    href.appendChild(linkText);
    href.href = location.origin + "/r/" + encodeURIComponent(recentRooms[i]);
    li.appendChild(href);
    this.roomRecentList_.appendChild(li);
    href.addEventListener("click", this.makeRecentlyUsedClickHandler_(recentRooms[i]).bind(this), false);
  }
};
RoomSelection.prototype.onRoomIdInput_ = function () {
  var room = this.roomIdInput_.value;
  var valid = room.length >= 5;
  var re = /^\w+$/;
  valid = valid && re.exec(room);
  if (valid) {
    this.roomJoinButton_.disabled = false;
    this.roomIdInput_.classList.remove("invalid");
    this.roomIdInputLabel_.classList.add("hidden");
  } else {
    this.roomJoinButton_.disabled = true;
    this.roomIdInput_.classList.add("invalid");
    this.roomIdInputLabel_.classList.remove("hidden");
  }
};
RoomSelection.prototype.onRoomIdKeyPress_ = function (event) {
  if (event.which !== 13 || this.roomJoinButton_.disabled) {
    return;
  }
  this.onJoinButton_();
};
RoomSelection.prototype.onRandomButton_ = function () {
  this.roomIdInput_.value = randomString(9);
  this.onRoomIdInput_();
};
RoomSelection.prototype.onJoinButton_ = function () {
  this.loadRoom_(this.roomIdInput_.value);
};

RoomSelection.prototype.onSettingButton_ = function () {
  var obj = $('#app_setting_panel')
  if (obj.classList.contains('hidden')) {
    //show setting

    obj.classList.remove('hidden');

  }
};

RoomSelection.prototype.makeRecentlyUsedClickHandler_ = function (roomName) {
  return function (e) {
    e.preventDefault();
    this.loadRoom_(roomName);
  };
};
RoomSelection.prototype.loadRoom_ = function (roomName) {
  this.recentlyUsedList_.pushRecentRoom(roomName);
  if (this.onRoomSelected) {
    this.onRoomSelected(roomName);
  }
};
RoomSelection.RecentlyUsedList = function (key) {
  this.LISTLENGTH_ = 10;
  this.RECENTROOMSKEY_ = key || "recentRooms";
  this.storage_ = new Storage;
};
RoomSelection.RecentlyUsedList.prototype.pushRecentRoom = function (roomId) {
  return new Promise(function (resolve, reject) {
    if (!roomId) {
      resolve();
      return;
    }
    this.getRecentRooms().then(function (recentRooms) {
      recentRooms = [roomId].concat(recentRooms);
      recentRooms = recentRooms.filter(function (value, index, self) {
        return self.indexOf(value) === index;
      });
      recentRooms = recentRooms.slice(0, this.LISTLENGTH_);
      this.storage_.setStorage(this.RECENTROOMSKEY_, JSON.stringify(recentRooms), function () {
        resolve();
      });
    }.bind(this)).catch(function (err) {
      reject(err);
    }.bind(this));
  }.bind(this));
};
RoomSelection.RecentlyUsedList.prototype.getRecentRooms = function () {
  return new Promise(function (resolve) {
    this.storage_.getStorage(this.RECENTROOMSKEY_, function (value) {
      var recentRooms = parseJSON(value);
      if (!recentRooms) {
        recentRooms = [];
      }
      resolve(recentRooms);
    });
  }.bind(this));
};
function mergeConstraints(cons1, cons2) {
  if (!cons1 || !cons2) {
    return cons1 || cons2;
  }
  var merged = cons1;
  for (var name in cons2.mandatory) {
    merged.mandatory[name] = cons2.mandatory[name];
  }
  merged.optional = merged.optional.concat(cons2.optional);
  return merged;
}
function iceCandidateType(candidateStr) {
  return candidateStr.split(" ")[7];
}
function maybeSetOpusOptions(sdp, params) {
  if (params.opusStereo === "true") {
    sdp = setCodecParam(sdp, "opus/48000", "stereo", "1");
  } else {
    if (params.opusStereo === "false") {
      sdp = removeCodecParam(sdp, "opus/48000", "stereo");
    }
  }
  if (params.opusFec === "true") {
    sdp = setCodecParam(sdp, "opus/48000", "useinbandfec", "1");
  } else {
    if (params.opusFec === "false") {
      sdp = removeCodecParam(sdp, "opus/48000", "useinbandfec");
    }
  }
  if (params.opusDtx === "true") {
    sdp = setCodecParam(sdp, "opus/48000", "usedtx", "1");
  } else {
    if (params.opusDtx === "false") {
      sdp = removeCodecParam(sdp, "opus/48000", "usedtx");
    }
  }
  if (params.opusMaxPbr) {
    sdp = setCodecParam(sdp, "opus/48000", "maxplaybackrate", params.opusMaxPbr);
  }
  return sdp;
}
function maybeSetAudioSendBitRate(sdp, params) {
  if (!params.audioSendBitrate) {
    return sdp;
  }
  trace("Prefer audio send bitrate: " + params.audioSendBitrate);
  return preferBitRate(sdp, params.audioSendBitrate, "audio");
}
function maybeSetAudioReceiveBitRate(sdp, params) {
  if (!params.audioRecvBitrate) {
    return sdp;
  }
  trace("Prefer audio receive bitrate: " + params.audioRecvBitrate);
  return preferBitRate(sdp, params.audioRecvBitrate, "audio");
}
function maybeSetVideoSendBitRate(sdp, params) {
  if (!params.videoSendBitrate) {
    return sdp;
  }
  trace("Prefer video send bitrate: " + params.videoSendBitrate);
  return preferBitRate(sdp, params.videoSendBitrate, "video");
}
function maybeSetVideoReceiveBitRate(sdp, params) {
  if (!params.videoRecvBitrate) {
    return sdp;
  }
  trace("Prefer video receive bitrate: " + params.videoRecvBitrate);
  return preferBitRate(sdp, params.videoRecvBitrate, "video");
}
function preferBitRate(sdp, bitrate, mediaType) {
  var sdpLines = sdp.split("\r\n");
  var mLineIndex = findLine(sdpLines, "m=", mediaType);
  if (mLineIndex === null) {
    trace("Failed to add bandwidth line to sdp, as no m-line found");
    return sdp;
  }
  var nextMLineIndex = findLineInRange(sdpLines, mLineIndex + 1, -1, "m=");
  if (nextMLineIndex === null) {
    nextMLineIndex = sdpLines.length;
  }
  var cLineIndex = findLineInRange(sdpLines, mLineIndex + 1, nextMLineIndex, "c=");
  if (cLineIndex === null) {
    trace("Failed to add bandwidth line to sdp, as no c-line found");
    return sdp;
  }
  var bLineIndex = findLineInRange(sdpLines, cLineIndex + 1, nextMLineIndex, "b=AS");
  if (bLineIndex) {
    sdpLines.splice(bLineIndex, 1);
  }
  var bwLine = "b=AS:" + bitrate;
  sdpLines.splice(cLineIndex + 1, 0, bwLine);
  sdp = sdpLines.join("\r\n");
  return sdp;
}
function maybeSetVideoSendInitialBitRate(sdp, params) {
  var initialBitrate = params.videoSendInitialBitrate;
  if (!initialBitrate) {
    return sdp;
  }
  var maxBitrate = initialBitrate;
  var bitrate = params.videoSendBitrate;
  if (bitrate) {
    if (initialBitrate > bitrate) {
      trace("Clamping initial bitrate to max bitrate of " + bitrate + " kbps.");
      initialBitrate = bitrate;
      params.videoSendInitialBitrate = initialBitrate;
    }
    maxBitrate = bitrate;
  }
  var sdpLines = sdp.split("\r\n");
  var mLineIndex = findLine(sdpLines, "m=", "video");
  if (mLineIndex === null) {
    trace("Failed to find video m-line");
    return sdp;
  }
  sdp = setCodecParam(sdp, "VP8/90000", "x-google-min-bitrate", params.videoSendInitialBitrate.toString());
  sdp = setCodecParam(sdp, "VP8/90000", "x-google-max-bitrate", maxBitrate.toString());
  return sdp;
}
function maybePreferAudioSendCodec(sdp, params) {
  return maybePreferCodec(sdp, "audio", "send", params.audioSendCodec);
}
function maybePreferAudioReceiveCodec(sdp, params) {
  return maybePreferCodec(sdp, "audio", "receive", params.audioRecvCodec);
}
function maybePreferVideoSendCodec(sdp, params) {
  return maybePreferCodec(sdp, "video", "send", params.videoSendCodec);
}
function maybePreferVideoReceiveCodec(sdp, params) {
  return maybePreferCodec(sdp, "video", "receive", params.videoRecvCodec);
}
function maybePreferCodec(sdp, type, dir, codec) {
  var str = type + " " + dir + " codec";
  if (codec === "") {
    trace("No preference on " + str + ".");
    return sdp;
  }
  trace("Prefer " + str + ": " + codec);
  var sdpLines = sdp.split("\r\n");
  var mLineIndex = findLine(sdpLines, "m=", type);
  if (mLineIndex === null) {
    return sdp;
  }
  var payload = getCodecPayloadType(sdpLines, codec);
  if (payload) {
    sdpLines[mLineIndex] = setDefaultCodec(sdpLines[mLineIndex], payload);
  }
  sdp = sdpLines.join("\r\n");
  return sdp;
}
function setCodecParam(sdp, codec, param, value) {
  var sdpLines = sdp.split("\r\n");
  var fmtpLineIndex = findFmtpLine(sdpLines, codec);
  var fmtpObj = {};
  if (fmtpLineIndex === null) {
    var index = findLine(sdpLines, "a=rtpmap", codec);
    if (index === null) {
      return sdp;
    }
    var payload = getCodecPayloadTypeFromLine(sdpLines[index]);
    fmtpObj.pt = payload.toString();
    fmtpObj.params = {};
    fmtpObj.params[param] = value;
    sdpLines.splice(index + 1, 0, writeFmtpLine(fmtpObj));
  } else {
    fmtpObj = parseFmtpLine(sdpLines[fmtpLineIndex]);
    fmtpObj.params[param] = value;
    sdpLines[fmtpLineIndex] = writeFmtpLine(fmtpObj);
  }
  sdp = sdpLines.join("\r\n");
  return sdp;
}
function removeCodecParam(sdp, codec, param) {
  var sdpLines = sdp.split("\r\n");
  var fmtpLineIndex = findFmtpLine(sdpLines, codec);
  if (fmtpLineIndex === null) {
    return sdp;
  }
  var map = parseFmtpLine(sdpLines[fmtpLineIndex]);
  delete map.params[param];
  var newLine = writeFmtpLine(map);
  if (newLine === null) {
    sdpLines.splice(fmtpLineIndex, 1);
  } else {
    sdpLines[fmtpLineIndex] = newLine;
  }
  sdp = sdpLines.join("\r\n");
  return sdp;
}
function parseFmtpLine(fmtpLine) {
  var fmtpObj = {};
  var spacePos = fmtpLine.indexOf(" ");
  var keyValues = fmtpLine.substring(spacePos + 1).split("; ");
  var pattern = new RegExp("a=fmtp:(\\d+)");
  var result = fmtpLine.match(pattern);
  if (result && result.length === 2) {
    fmtpObj.pt = result[1];
  } else {
    return null;
  }
  var params = {};
  for (var i = 0; i < keyValues.length; ++i) {
    var pair = keyValues[i].split("=");
    if (pair.length === 2) {
      params[pair[0]] = pair[1];
    }
  }
  fmtpObj.params = params;
  return fmtpObj;
}
function writeFmtpLine(fmtpObj) {
  if (!fmtpObj.hasOwnProperty("pt") || !fmtpObj.hasOwnProperty("params")) {
    return null;
  }
  var pt = fmtpObj.pt;
  var params = fmtpObj.params;
  var keyValues = [];
  var i = 0;
  for (var key in params) {
    keyValues[i] = key + "=" + params[key];
    ++i;
  }
  if (i === 0) {
    return null;
  }
  return "a=fmtp:" + pt.toString() + " " + keyValues.join("; ");
}
function findFmtpLine(sdpLines, codec) {
  var payload = getCodecPayloadType(sdpLines, codec);
  return payload ? findLine(sdpLines, "a=fmtp:" + payload.toString()) : null;
}
function findLine(sdpLines, prefix, substr) {
  return findLineInRange(sdpLines, 0, -1, prefix, substr);
}
function findLineInRange(sdpLines, startLine, endLine, prefix, substr) {
  var realEndLine = endLine !== -1 ? endLine : sdpLines.length;
  for (var i = startLine; i < realEndLine; ++i) {
    if (sdpLines[i].indexOf(prefix) === 0) {
      if (!substr || sdpLines[i].toLowerCase().indexOf(substr.toLowerCase()) !== -1) {
        return i;
      }
    }
  }
  return null;
}
function getCodecPayloadType(sdpLines, codec) {
  var index = findLine(sdpLines, "a=rtpmap", codec);
  return index ? getCodecPayloadTypeFromLine(sdpLines[index]) : null;
}
function getCodecPayloadTypeFromLine(sdpLine) {
  var pattern = new RegExp("a=rtpmap:(\\d+) \\w+\\/\\d+");
  var result = sdpLine.match(pattern);
  return result && result.length === 2 ? result[1] : null;
}
function setDefaultCodec(mLine, payload) {
  var elements = mLine.split(" ");
  var newLine = elements.slice(0, 3);
  newLine.push(payload);
  for (var i = 3; i < elements.length; i++) {
    if (elements[i] !== payload) {
      newLine.push(elements[i]);
    }
  }
  return newLine.join(" ");
}
; var SignalingChannel = function (wssUrl, wssPostUrl) {
  this.wssUrl_ = wssUrl;
  this.wssPostUrl_ = wssPostUrl;
  this.roomId_ = null;
  this.clientId_ = null;
  this.websocket_ = null;
  this.registered_ = false;
  this.onerror = null;
  this.onmessage = null;
};
SignalingChannel.prototype.open = function () {
  if (this.websocket_) {
    trace("ERROR: SignalingChannel has already opened.");
    return;
  }
  trace("Opening signaling channel.");
  return new Promise(function (resolve, reject) {
    if (isChromeApp()) {
      this.websocket_ = new RemoteWebSocket(this.wssUrl_, this.wssPostUrl_);
    } else {
      this.websocket_ = new WebSocket(this.wssUrl_);
    }
    this.websocket_.onopen = function () {
      trace("Signaling channel opened.");
      this.websocket_.onerror = function () {
        trace("Signaling channel error.");
      };
      this.websocket_.onclose = function (event) {
        trace("Channel closed with code:" + event.code + " reason:" + event.reason);
        this.websocket_ = null;
        this.registered_ = false;
      };
      if (this.clientId_ && this.roomId_) {
        this.register(this.roomId_, this.clientId_);
      }
      resolve();
    }.bind(this);
    this.websocket_.onmessage = function (event) {
      trace("WSS->C: " + event.data);
      var message = parseJSON(event.data);
      if (!message) {
        trace("Failed to parse WSS message: " + event.data);
        return;
      }
      if (message.error) {
        trace("Signaling server error message: " + message.error);
        return;
      }
      this.onmessage(message.msg);
    }.bind(this);
    this.websocket_.onerror = function () {
      reject(Error("WebSocket error."));
    };
  }.bind(this));
};
SignalingChannel.prototype.register = function (roomId, clientId) {
  if (this.registered_) {
    trace("ERROR: SignalingChannel has already registered.");
    return;
  }
  this.roomId_ = roomId;
  this.clientId_ = clientId;
  if (!this.roomId_) {
    trace("ERROR: missing roomId.");
  }
  if (!this.clientId_) {
    trace("ERROR: missing clientId.");
  }
  if (!this.websocket_ || this.websocket_.readyState !== WebSocket.OPEN) {
    trace("WebSocket not open yet; saving the IDs to register later.");
    return;
  }
  trace("Registering signaling channel.");
  var registerMessage = { cmd: "register", roomid: this.roomId_, clientid: this.clientId_ };
  this.websocket_.send(JSON.stringify(registerMessage));
  this.registered_ = true;
  trace("Signaling channel registered.");
};
SignalingChannel.prototype.close = function (async) {
  if (this.websocket_) {
    this.websocket_.close();
    this.websocket_ = null;
  }
  if (!this.clientId_ || !this.roomId_) {
    return;
  }
  var path = this.getWssPostUrl();
  return sendUrlRequest("DELETE", path, async).catch(function (error) {
    trace("Error deleting web socket connection: " + error.message);
  }.bind(this)).then(function () {
    this.clientId_ = null;
    this.roomId_ = null;
    this.registered_ = false;
  }.bind(this));
};
SignalingChannel.prototype.send = function (message) {
  if (!this.roomId_ || !this.clientId_) {
    trace("ERROR: SignalingChannel has not registered.");
    return;
  }
  trace("C->WSS: " + message);
  var wssMessage = { cmd: "send", msg: message };
  var msgString = JSON.stringify(wssMessage);
  if (this.websocket_ && this.websocket_.readyState === WebSocket.OPEN) {
    this.websocket_.send(msgString);
  } else {
    var path = this.getWssPostUrl();
    var xhr = new XMLHttpRequest;
    xhr.open("POST", path, true);
    xhr.send(wssMessage.msg);
  }
};
SignalingChannel.prototype.getWssPostUrl = function () {
  return this.wssPostUrl_ + "/" + this.roomId_ + "/" + this.clientId_;
};
function extractStatAsInt(stats, statObj, statName) {
  var str = extractStat(stats, statObj, statName);
  if (str) {
    var val = parseInt(str);
    if (val !== -1) {
      return val;
    }
  }
  return null;
}
function extractStat(stats, statObj, statName) {
  var report = getStatsReport(stats, statObj, statName);
  if (report && report.names().indexOf(statName) !== -1) {
    return report.stat(statName);
  }
  return null;
}
function getStatsReport(stats, statObj, statName, statVal) {
  if (stats) {
    for (var i = 0; i < stats.length; ++i) {
      var report = stats[i];
      if (report.type === statObj) {
        var found = true;
        if (statName) {
          var val = report.stat(statName);
          found = statVal !== undefined ? val === statVal : val;
        }
        if (found) {
          return report;
        }
      }
    }
  }
}
function computeRate(newReport, oldReport, statName) {
  var newVal = newReport.stat(statName);
  var oldVal = oldReport ? oldReport.stat(statName) : null;
  if (newVal === null || oldVal === null) {
    return null;
  }
  return (newVal - oldVal) / (newReport.timestamp - oldReport.timestamp) * 1E3;
}
function computeBitrate(newReport, oldReport, statName) {
  return computeRate(newReport, oldReport, statName) * 8;
}
function computeE2EDelay(captureStart, remoteVideoCurrentTime) {
  if (!captureStart) {
    return null;
  }
  var nowNTP = Date.now() + 22089888E5;
  return nowNTP - captureStart - remoteVideoCurrentTime * 1E3;
}
; var Storage = function () {
};
Storage.prototype.getStorage = function (key, callback) {
  if (isChromeApp()) {
    chrome.storage.local.get(key, function (values) {
      if (callback) {
        window.setTimeout(function () {
          callback(values[key]);
        }, 0);
      }
    });
  } else {
    var value = localStorage.getItem(key);
    if (callback) {
      window.setTimeout(function () {
        callback(value);
      }, 0);
    }
  }
};
Storage.prototype.setStorage = function (key, value, callback) {
  if (isChromeApp()) {
    var data = {};
    data[key] = value;
    chrome.storage.local.set(data, callback);
  } else {
    localStorage.setItem(key, value);
    if (callback) {
      window.setTimeout(callback, 0);
    }
  }
};
function $(selector) {
  return document.querySelector(selector);
}
function queryStringToDictionary(queryString) {
  var pairs = queryString.slice(1).split("&");
  var result = {};
  pairs.forEach(function (pair) {
    if (pair) {
      pair = pair.split("=");
      if (pair[0]) {
        result[pair[0]] = decodeURIComponent(pair[1] || "");
      }
    }
  });
  return result;
}
function sendAsyncUrlRequest(method, url, body) {
  return sendUrlRequest(method, url, true, body);
}
function sendUrlRequest(method, url, async, body) {
  return new Promise(function (resolve, reject) {
    var xhr;
    var reportResults = function () {
      if (xhr.status !== 200) {
        reject(Error("Status=" + xhr.status + ", response=" + xhr.responseText));
        return;
      }
      resolve(xhr.responseText);
    };
    xhr = new XMLHttpRequest;
    if (async) {
      xhr.onreadystatechange = function () {
        if (xhr.readyState !== 4) {
          return;
        }
        reportResults();
      };
    }
    xhr.open(method, url, async);
    xhr.send(body);
    if (!async) {
      reportResults();
    }
  });
}
function requestTurnServers(turnRequestUrl, turnTransports) {
  return new Promise(function (resolve, reject) {
    var method = isChromeApp() ? "POST" : "GET";
    sendAsyncUrlRequest(method, turnRequestUrl).then(function (response) {
      var turnServerResponse = parseJSON(response);
      if (!turnServerResponse) {
        reject(Error("Error parsing response JSON: " + response));
        return;
      }
      if (turnTransports && turnTransports.length > 0) {
        filterTurnUrls(turnServerResponse.uris, turnTransports);
      }
      var turnServers = { urls: turnServerResponse.uris, username: turnServerResponse.username, credential: turnServerResponse.password };
      trace("Retrieved TURN server information.");
      resolve(turnServers);
    }).catch(function (error) {
      reject(Error("TURN server request error: " + error.message));
      return;
    });
  });
}
function parseJSON(json) {
  try {
    return JSON.parse(json);
  } catch (e) {
    trace("Error parsing json: " + json);
  }
  return null;
}
function filterTurnUrls(urls, protocol) {
  for (var i = 0; i < urls.length;) {
    var parts = urls[i].split("?");
    if (parts.length > 1 && parts[1] !== "transport=" + protocol) {
      urls.splice(i, 1);
    } else {
      ++i;
    }
  }
}
function setUpFullScreen() {
  if (isChromeApp()) {
    document.cancelFullScreen = function () {
      chrome.app.window.current().restore();
    };
  } else {
    document.cancelFullScreen = document.webkitCancelFullScreen || document.mozCancelFullScreen || document.cancelFullScreen;
  }
  if (isChromeApp()) {
    document.body.requestFullScreen = function () {
      chrome.app.window.current().fullscreen();
    };
  } else {
    document.body.requestFullScreen = document.body.webkitRequestFullScreen || document.body.mozRequestFullScreen || document.body.requestFullScreen;
  }
  document.onfullscreenchange = document.onfullscreenchange || document.onwebkitfullscreenchange || document.onmozfullscreenchange;
}
function isFullScreen() {
  if (isChromeApp()) {
    return chrome.app.window.current().isFullscreen();
  }
  return !!(document.webkitIsFullScreen || document.mozFullScreen || document.isFullScreen);
}
function fullScreenElement() {
  return document.webkitFullScreenElement || document.webkitCurrentFullScreenElement || document.mozFullScreenElement || document.fullScreenElement;
}
function randomString(strLength) {
  var result = [];
  strLength = strLength || 5;
  var charSet = "0123456789";
  while (strLength--) {
    result.push(charSet.charAt(Math.floor(Math.random() * charSet.length)));
  }
  return result.join("");
}
function isChromeApp() {
  return typeof chrome !== "undefined" && typeof chrome.storage !== "undefined" && typeof chrome.storage.local !== "undefined";
}
; var apprtc = apprtc || {};
apprtc.windowPort = apprtc.windowPort || {};
(function () {
  var port_;
  apprtc.windowPort.sendMessage = function (message) {
    var port = getPort_();
    try {
      port.postMessage(message);
    } catch (ex) {
      trace("Error sending message via port: " + ex);
    }
  };
  apprtc.windowPort.addMessageListener = function (listener) {
    var port = getPort_();
    port.onMessage.addListener(listener);
  };
  var getPort_ = function () {
    if (!port_) {
      port_ = chrome.runtime.connect();
    }
    return port_;
  };
})();
