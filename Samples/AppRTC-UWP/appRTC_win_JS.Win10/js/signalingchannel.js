/*
 *  Copyright (c) 2014 The WebRTC project authors. All Rights Reserved.
 *
 *  Use of this source code is governed by a BSD-style license
 *  that can be found in the LICENSE file in the root of the source
 *  tree.
 */

/* More information about these options at jshint.com/docs/options */

/* globals parseJSON, trace, sendUrlRequest, isChromeApp, RemoteWebSocket */
/* exported SignallingChannel */

'use strict';

// This class implements a signalling channel based on WebSocket.
var SignallingChannel = function(wssUrl, wssPostUrl) {
  this.wssUrl_ = wssUrl;
  this.wssPostUrl_ = wssPostUrl;
  this.roomId_ = null;
  this.clientId_ = null;
  this.websocket_ = null;
  this.registered_ = false;

  // Public callbacks. Keep it sorted.
  this.onerror = null;
  this.onmessage = null;
};

SignallingChannel.prototype.open = function() {
  if (this.websocket_) {
    trace('ERROR: SignallingChannel has already opened.');
    return;
  }

  trace('Opening signalling channel.');
  return new Promise(function(resolve, reject) {
    if (isChromeApp()) {
      this.websocket_ = new RemoteWebSocket(this.wssUrl_, this.wssPostUrl_);
    } else {
      this.websocket_ = new WebSocket(this.wssUrl_);
    }

    this.websocket_.onopen = function() {
      trace('Signalling channel opened.');

      this.websocket_.onerror = function() {
        trace('Signalling channel error.');
      };
      this.websocket_.onclose = function(event) {
        // TODO(tkchin): reconnect to WSS.
        trace('Channel closed with code:' + event.code +
            ' reason:' + event.reason);
        this.websocket_ = null;
        this.registered_ = false;
      };

      if (this.clientId_ && this.roomId_) {
        this.register(this.roomId_, this.clientId_);
      }

      resolve();
    }.bind(this);

    this.websocket_.onmessage = function(event) {
      trace('WSS->C: ' + event.data);

      var message = parseJSON(event.data);
      if (!message) {
        trace('Failed to parse WSS message: ' + event.data);
        return;
      }
      if (message.error) {
        trace('Signalling server error message: ' + message.error);
        return;
      }
      this.onmessage(message.msg);
    }.bind(this);

    this.websocket_.onerror = function() {
      reject(Error('WebSocket error.'));
    };
  }.bind(this));
};

SignallingChannel.prototype.register = function(roomId, clientId) {
  if (this.registered_) {
    trace('ERROR: SignallingChannel has already registered.');
    return;
  }

  this.roomId_ = roomId;
  this.clientId_ = clientId;

  if (!this.roomId_) {
    trace('ERROR: missing roomId.');
  }
  if (!this.clientId_) {
    trace('ERROR: missing clientId.');
  }
  if (!this.websocket_ || this.websocket_.readyState !== WebSocket.OPEN) {
    trace('WebSocket not open yet; saving the IDs to register later.');
    return;
  }
  trace('Registering signalling channel.');
  var registerMessage = {
    cmd: 'register',
    roomid: this.roomId_,
    clientid: this.clientId_
  };
  this.websocket_.send(JSON.stringify(registerMessage));
  this.registered_ = true;

  // TODO(tkchin): Better notion of whether registration succeeded. Basically
  // check that we don't get an error message back from the socket.
  trace('Signalling channel registered.');
};

SignallingChannel.prototype.close = function(async) {
  if (this.websocket_) {
    this.websocket_.close();
    this.websocket_ = null;
  }

  if (!this.clientId_ || !this.roomId_) {
    return;
  }
  // Tell WSS that we're done.
  var path = this.getWssPostUrl();

  return sendUrlRequest('DELETE', path, async).catch(function(error) {
    trace('Error deleting web socket connection: ' + error.message);
  }.bind(this)).then(function() {
    this.clientId_ = null;
    this.roomId_ = null;
    this.registered_ = false;
  }.bind(this));
};

SignallingChannel.prototype.send = function(message) {
  if (!this.roomId_ || !this.clientId_) {
    trace('ERROR: SignallingChannel has not registered.');
    return;
  }
  trace('C->WSS: ' + message);

  var wssMessage = {
    cmd: 'send',
    msg: message
  };
  var msgString = JSON.stringify(wssMessage);

  if (this.websocket_ && this.websocket_.readyState === WebSocket.OPEN) {
    this.websocket_.send(msgString);
  } else {
    var path = this.getWssPostUrl();
    var xhr = new XMLHttpRequest();
    xhr.open('POST', path, true);
    xhr.send(wssMessage.msg);
  }
};

SignallingChannel.prototype.getWssPostUrl = function() {
  return this.wssPostUrl_ + '/' + this.roomId_ + '/' + this.clientId_;
};
