﻿(function () {

    // Reports media playback to the device for lock screen control

    var currentPlayer;
    var lastPlayerState;
    var lastUpdateTime = 0;

    function updatePlayerState(state, eventName) {

        if (!state.NowPlayingItem) {
            hideMediaControls();
            return;
        }

        // dummy this up
        if (eventName == 'init') {
            eventName = 'positionchange';
        }

        lastPlayerState = state;

        var playState = state.PlayState || {};

        var nameHtml = MediaController.getNowPlayingNameHtml(state.NowPlayingItem) || '';
        var parts = nameHtml.split('<br/>');

        var artist = parts.length == 1 ? '' : parts[0];
        var title = parts[parts.length - 1];
        var album = state.NowPlayingItem.Album || '';
        var duration = state.NowPlayingItem.RunTimeTicks ? (state.NowPlayingItem.RunTimeTicks / 10000000) : 0;
        var position = playState.PositionTicks ? (playState.PositionTicks / 10000000) : 0;
        var itemId = state.NowPlayingItem.Id;

        var isPaused = playState.IsPaused || false;
        var canSeek = playState.CanSeek || false;

        var url = '';
        var imgHeight = 400;

        var nowPlayingItem = state.NowPlayingItem;

        if (nowPlayingItem.PrimaryImageTag) {

            url = ApiClient.getScaledImageUrl(nowPlayingItem.PrimaryImageItemId, {
                type: "Primary",
                height: imgHeight,
                tag: nowPlayingItem.PrimaryImageTag
            });
        } else if (nowPlayingItem.ThumbImageTag) {

            url = ApiClient.getScaledImageUrl(nowPlayingItem.ThumbImageItemId, {
                type: "Thumb",
                height: imgHeight,
                tag: nowPlayingItem.ThumbImageTag
            });
        }
        else if (nowPlayingItem.BackdropImageTag) {

            url = ApiClient.getScaledImageUrl(nowPlayingItem.BackdropItemId, {
                type: "Backdrop",
                height: imgHeight,
                tag: nowPlayingItem.BackdropImageTag,
                index: 0
            });

        }

        // Don't go crazy reporting position changes
        if (eventName == 'positionchange') {
            if (lastUpdateTime) {
                // Only report if this item hasn't been reported yet, or if there's an actual playback change.
                // Don't report on simple time updates
                return;
            }
        }

        var isLocalPlayer = MediaController.getPlayerInfo().isLocalPlayer || false;

        MainActivity.updateMediaSession(eventName, isLocalPlayer, itemId, title, artist, album, parseInt(duration), parseInt(position), url, canSeek, isPaused);
        lastUpdateTime = new Date().getTime();
    }

    function onStateChanged(e, state) {

        updatePlayerState(state, e.type);
    }

    function onPlaybackStart(e, state) {

        console.log('nowplaying event: ' + e.type);

        var player = this;

        player.beginPlayerUpdates();

        onStateChanged.call(player, e, state);
    }

    function onPlaybackStopped(e, state) {

        console.log('nowplaying event: ' + e.type);
        var player = this;

        player.endPlayerUpdates();

        hideMediaControls();
    }

    function releaseCurrentPlayer() {

        if (currentPlayer) {

            $(currentPlayer).off('.cordovaremote');
            currentPlayer.endPlayerUpdates();
            currentPlayer = null;

            hideMediaControls();
        }
    }

    function hideMediaControls() {
        MainActivity.hideMediaSession();
        lastUpdateTime = 0;
    }

    function bindToPlayer(player) {

        releaseCurrentPlayer();

        currentPlayer = player;

        console.log('binding remotecontrols to MediaPlayer');

        player.getPlayerState().done(function (state) {

            if (state.NowPlayingItem) {
                player.beginPlayerUpdates();
            }

            onStateChanged.call(player, { type: 'init' }, state);
        });

        $(player).on('playbackstart.cordovaremote', onPlaybackStart)
            .on('playbackstop.cordovaremote', onPlaybackStopped)
            .on('playstatechange.cordovaremote', onStateChanged)
            .on('positionchange.cordovaremote', onStateChanged);
    }

    Dashboard.ready(function () {

        console.log('binding remotecontrols to MediaController');

        $(MediaController).on('playerchange', function () {

            bindToPlayer(MediaController.getCurrentPlayer());
        });

        bindToPlayer(MediaController.getCurrentPlayer());

    });

})();