define(["browser","require","events","apphost","loading","dom","playbackManager","embyRouter","appSettings","connectionManager","./htmlmediahelper"],function(browser,require,events,appHost,loading,dom,playbackManager,embyRouter,appSettings,connectionManager,htmlMediaHelper){"use strict";function tryRemoveElement(elem){var parentNode=elem.parentNode;if(parentNode)try{parentNode.removeChild(elem)}catch(err){console.log("Error removing dialog element: "+err)}}function getDefaultProfile(){return new Promise(function(resolve,reject){require(["browserdeviceprofile"],function(profileBuilder){resolve(profileBuilder({}))})})}function enableNativeTrackSupport(currentSrc,track){if(browser.firefox&&(currentSrc||"").toLowerCase().indexOf(".m3u8")!==-1)return!1;if(browser.ps4)return!1;if(browser.edge)return!1;if(track){var format=(track.Codec||"").toLowerCase();if("ssa"===format||"ass"===format)return!1}return!0}function requireHlsPlayer(callback){require(["hlsjs"],function(hls){window.Hls=hls,callback()})}function getMediaStreamAudioTracks(mediaSource){return mediaSource.MediaStreams.filter(function(s){return"Audio"===s.Type})}function getMediaStreamTextTracks(mediaSource){return mediaSource.MediaStreams.filter(function(s){return"Subtitle"===s.Type&&"External"===s.DeliveryMethod})}function zoomIn(elem){return new Promise(function(resolve,reject){var duration=240;elem.style.animation="htmlvideoplayer-zoomin "+duration+"ms ease-in normal",dom.addEventListener(elem,dom.whichAnimationEvent(),resolve,{once:!0})})}function normalizeTrackEventText(text){return text.replace(/\\N/gi,"\n")}function setTracks(elem,tracks,mediaSource,serverId){elem.innerHTML=getTracksHtml(tracks,mediaSource,serverId)}function getTextTrackUrl(track,serverId){return playbackManager.getSubtitleUrl(track,serverId)}function getTracksHtml(tracks,mediaSource,serverId){return tracks.map(function(t){var defaultAttribute=mediaSource.DefaultSubtitleStreamIndex===t.Index?" default":"",language=t.Language||"und",label=t.Language||"und";return'<track id="textTrack'+t.Index+'" label="'+label+'" kind="subtitles" src="'+getTextTrackUrl(t,serverId)+'" srclang="'+language+'"'+defaultAttribute+"></track>"}).join("")}function HtmlVideoPlayer(){function updateVideoUrl(streamInfo){var isHls=streamInfo.url.toLowerCase().indexOf(".m3u8")!==-1,mediaSource=streamInfo.mediaSource,item=streamInfo.item;if(mediaSource&&item&&!mediaSource.RunTimeTicks&&isHls&&"Transcode"===streamInfo.playMethod&&(browser.iOS||browser.osx)){var hlsPlaylistUrl=streamInfo.url.replace("master.m3u8","live.m3u8");return loading.show(),console.log("prefetching hls playlist: "+hlsPlaylistUrl),connectionManager.getApiClient(item.ServerId).ajax({type:"GET",url:hlsPlaylistUrl}).then(function(){return console.log("completed prefetching hls playlist: "+hlsPlaylistUrl),loading.hide(),streamInfo.url=hlsPlaylistUrl,Promise.resolve()},function(){return console.log("error prefetching hls playlist: "+hlsPlaylistUrl),loading.hide(),Promise.resolve()})}return Promise.resolve()}function setCurrentSrc(elem,options){elem.removeEventListener("error",onError);var val=options.url;console.log("playing url: "+val);var seconds=(options.playerStartPositionTicks||0)/1e7;seconds&&(val+="#t="+seconds),htmlMediaHelper.destroyHlsPlayer(self);for(var tracks=getMediaStreamTextTracks(options.mediaSource),currentTrackIndex=-1,i=0,length=tracks.length;i<length;i++)if(tracks[i].Index===options.mediaSource.DefaultSubtitleStreamIndex){currentTrackIndex=tracks[i].Index;break}subtitleTrackIndexToSetOnPlaying=currentTrackIndex,self._currentPlayOptions=options;var crossOrigin=htmlMediaHelper.getCrossOriginValue(options.mediaSource);if(crossOrigin&&(elem.crossOrigin=crossOrigin),htmlMediaHelper.enableHlsPlayer(options.item,options.mediaSource,"Video")&&val.indexOf(".m3u8")!==-1)return setTracks(elem,tracks,options.mediaSource,options.item.ServerId),new Promise(function(resolve,reject){requireHlsPlayer(function(){var hls=new Hls({manifestLoadingTimeOut:2e4});hls.loadSource(val),hls.attachMedia(elem),htmlMediaHelper.bindEventsToHlsPlayer(self,hls,elem,onError,resolve,reject),self._hlsPlayer=hls,self._currentSrc=val,setCurrentTrackElement(currentTrackIndex)})});elem.autoplay=!0;var mimeType=options.mimeType;return mimeType&&browser.operaTv?(browser.chrome&&"video/x-matroska"===mimeType&&(mimeType="video/webm"),elem.currentSrc&&(elem.src="",elem.removeAttribute("src")),elem.innerHTML='<source src="'+val+'" type="'+mimeType+'">'+getTracksHtml(tracks,options.mediaSource,options.item.ServerId),elem.addEventListener("loadedmetadata",onLoadedMetadata),self._currentSrc=val,setCurrentTrackElement(currentTrackIndex),htmlMediaHelper.playWithPromise(elem,onError)):htmlMediaHelper.applySrc(elem,val,options).then(function(){return setTracks(elem,tracks,options.mediaSource,options.item.ServerId),self._currentSrc=val,setCurrentTrackElement(currentTrackIndex),htmlMediaHelper.playWithPromise(elem,onError)})}function onEnded(){destroyCustomTrack(this),htmlMediaHelper.onEndedInternal(self,this,onError)}function onTimeUpdate(e){var time=this.currentTime;time&&!self._timeUpdated&&(self._timeUpdated=!0,ensureValidVideo(this)),self._currentTime=time;var currentPlayOptions=self._currentPlayOptions;if(currentPlayOptions){var timeMs=1e3*time;timeMs+=(currentPlayOptions.transcodingOffsetTicks||0)/1e4,updateSubtitleText(timeMs)}events.trigger(self,"timeupdate")}function onVolumeChange(){htmlMediaHelper.saveVolume(this.volume),events.trigger(self,"volumechange")}function onNavigatedToOsd(){videoDialog.classList.remove("videoPlayerContainer-withBackdrop"),videoDialog.classList.remove("videoPlayerContainer-onTop")}function onPlaying(e){self._started||(self._started=!0,this.removeAttribute("controls"),self._currentPlayOptions.title?(self.originalDocumentTitle=document.title,document.title=self._currentPlayOptions.title):self.originalDocumentTitle=null,setCurrentTrackElement(subtitleTrackIndexToSetOnPlaying),htmlMediaHelper.seekOnPlaybackStart(self,e.target,self._currentPlayOptions.playerStartPositionTicks),self._currentPlayOptions.fullscreen?embyRouter.showVideoOsd().then(onNavigatedToOsd):(embyRouter.setTransparency("backdrop"),videoDialog.classList.remove("videoPlayerContainer-withBackdrop"),videoDialog.classList.remove("videoPlayerContainer-onTop")),loading.hide()),events.trigger(self,"playing")}function onPlay(e){events.trigger(self,"unpause")}function ensureValidVideo(elem){if(elem===self._mediaElement)return 0===elem.videoWidth&&0===elem.videoHeight?void htmlMediaHelper.onErrorInternal(self,"mediadecodeerror"):void 0}function onClick(){events.trigger(self,"click")}function onDblClick(){events.trigger(self,"dblclick")}function onPause(){events.trigger(self,"pause")}function onError(){var errorCode=this.error?this.error.code||0:0,errorMessage=this.error?this.error.message||"":"";console.log("Media element error: "+errorCode.toString()+" "+errorMessage);var type;switch(errorCode){case 1:return;case 2:type="network";break;case 3:if(self._hlsPlayer)return void htmlMediaHelper.handleMediaError(self);type="mediadecodeerror";break;case 4:type="medianotsupported";break;default:return}htmlMediaHelper.onErrorInternal(self,type)}function onLoadedMetadata(e){var mediaElem=e.target;if(mediaElem.removeEventListener("loadedmetadata",onLoadedMetadata),!self._hlsPlayer)try{mediaElem.play()}catch(err){console.log("error calling mediaElement.play: "+err)}}function destroyCustomTrack(videoElement){if(window.removeEventListener("resize",onVideoResize),window.removeEventListener("orientationchange",onVideoResize),videoSubtitlesElem){var subtitlesContainer=videoSubtitlesElem.parentNode;subtitlesContainer&&tryRemoveElement(subtitlesContainer),videoSubtitlesElem=null}if(currentTrackEvents=null,videoElement)for(var allTracks=videoElement.textTracks||[],i=0;i<allTracks.length;i++){var currentTrack=allTracks[i];currentTrack.label.indexOf("manualTrack")!==-1&&(currentTrack.mode="disabled")}customTrackIndex=-1,currentClock=null,self._currentAspectRatio=null;var renderer=currentAssRenderer;renderer&&renderer.setEnabled(!1),currentAssRenderer=null}function fetchSubtitles(track,serverId){return new Promise(function(resolve,reject){require(["fetchHelper"],function(fetchHelper){fetchHelper.ajax({url:getTextTrackUrl(track,serverId).replace(".vtt",".js"),type:"GET",dataType:"json"}).then(resolve,reject)})})}function setTrackForCustomDisplay(videoElement,track){if(!track)return void destroyCustomTrack(videoElement);if(customTrackIndex!==track.Index){var serverId=self._currentPlayOptions.item.ServerId;destroyCustomTrack(videoElement),customTrackIndex=track.Index,renderTracksEvents(videoElement,track,serverId),lastCustomTrackMs=0}}function renderWithLibjass(videoElement,track,serverId){var rendererSettings={};browser.ps4?rendererSettings.enableSvg=!1:(browser.edge||browser.msie)&&(rendererSettings.enableSvg=!1),require(["libjass"],function(libjass){libjass.ASS.fromUrl(getTextTrackUrl(track,serverId)).then(function(ass){var clock=new libjass.renderers.ManualClock;currentClock=clock;var renderer=new libjass.renderers.WebRenderer(ass,clock,videoElement.parentNode,rendererSettings);currentAssRenderer=renderer,renderer.addEventListener("ready",function(){try{renderer.resize(videoElement.offsetWidth,videoElement.offsetHeight,0,0),window.removeEventListener("resize",onVideoResize),window.addEventListener("resize",onVideoResize),window.removeEventListener("orientationchange",onVideoResize),window.addEventListener("orientationchange",onVideoResize)}catch(ex){}})},function(){htmlMediaHelper.onErrorInternal(self,"mediadecodeerror")})})}function onVideoResize(){var renderer=currentAssRenderer;if(renderer){var videoElement=self._mediaElement,width=videoElement.offsetWidth,height=videoElement.offsetHeight;console.log("videoElement resized: "+width+"x"+height),renderer.resize(width,height,0,0)}}function requiresCustomSubtitlesElement(){return!!browser.ps4||(!!browser.firefox||!!browser.edge)}function renderSubtitlesWithCustomElement(videoElement,track,serverId){fetchSubtitles(track,serverId).then(function(data){if(!videoSubtitlesElem){var subtitlesContainer=document.createElement("div");subtitlesContainer.classList.add("videoSubtitles"),subtitlesContainer.innerHTML='<div class="videoSubtitlesInner"></div>',videoSubtitlesElem=subtitlesContainer.querySelector(".videoSubtitlesInner"),videoElement.parentNode.appendChild(subtitlesContainer),currentTrackEvents=data.TrackEvents}})}function renderTracksEvents(videoElement,track,serverId){var format=(track.Codec||"").toLowerCase();if("ssa"===format||"ass"===format)return void renderWithLibjass(videoElement,track,serverId);if(requiresCustomSubtitlesElement())return void renderSubtitlesWithCustomElement(videoElement,track,serverId);for(var trackElement=null,expectedId="manualTrack"+track.Index,allTracks=videoElement.textTracks,i=0;i<allTracks.length;i++){var currentTrack=allTracks[i];if(currentTrack.label===expectedId){trackElement=currentTrack;break}currentTrack.mode="disabled"}trackElement?trackElement.mode="showing":(trackElement=videoElement.addTextTrack("subtitles","manualTrack"+track.Index,track.Language||"und"),fetchSubtitles(track,serverId).then(function(data){console.log("downloaded "+data.TrackEvents.length+" track events"),data.TrackEvents.forEach(function(trackEvent){var trackCueObject=window.VTTCue||window.TextTrackCue,cue=new trackCueObject(trackEvent.StartPositionTicks/1e7,trackEvent.EndPositionTicks/1e7,normalizeTrackEventText(trackEvent.Text));trackElement.addCue(cue)}),trackElement.mode="showing"}))}function updateSubtitleText(timeMs){var clock=currentClock;if(clock)try{clock.seek(timeMs/1e3)}catch(err){console.log("Error in libjass: "+err)}else{var trackEvents=currentTrackEvents,subtitleTextElement=videoSubtitlesElem;if(trackEvents&&subtitleTextElement){for(var selectedTrackEvent,ticks=1e4*timeMs,i=0;i<trackEvents.length;i++){var currentTrackEvent=trackEvents[i];if(currentTrackEvent.StartPositionTicks<=ticks&&currentTrackEvent.EndPositionTicks>=ticks){selectedTrackEvent=currentTrackEvent;break}}selectedTrackEvent&&selectedTrackEvent.Text?(subtitleTextElement.innerHTML=normalizeTrackEventText(selectedTrackEvent.Text),subtitleTextElement.classList.remove("hide")):subtitleTextElement.classList.add("hide")}}}function setCurrentTrackElement(streamIndex){console.log("Setting new text track index to: "+streamIndex);var mediaStreamTextTracks=getMediaStreamTextTracks(self._currentPlayOptions.mediaSource),track=streamIndex===-1?null:mediaStreamTextTracks.filter(function(t){return t.Index===streamIndex})[0];enableNativeTrackSupport(self._currentSrc,track)?setTrackForCustomDisplay(self._mediaElement,null):(setTrackForCustomDisplay(self._mediaElement,track),streamIndex=-1,track=null);for(var expectedId="textTrack"+streamIndex,trackIndex=streamIndex!==-1&&track?mediaStreamTextTracks.indexOf(track):-1,modes=["disabled","showing","hidden"],allTracks=self._mediaElement.textTracks,i=0;i<allTracks.length;i++){var currentTrack=allTracks[i];console.log("currentTrack id: "+currentTrack.id);var mode;if(console.log("expectedId: "+expectedId+"--currentTrack.Id:"+currentTrack.id),browser.msie||browser.edge)mode=trackIndex===i?1:0;else{if(currentTrack.label.indexOf("manualTrack")!==-1)continue;mode=currentTrack.id===expectedId?1:0}console.log("Setting track "+i+" mode to: "+mode);var useNumericMode=!1;!isNaN(currentTrack.mode),useNumericMode?currentTrack.mode=mode:currentTrack.mode=modes[mode]}}function createMediaElement(options){return(browser.tv||browser.noAnimation||browser.iOS)&&(options.backdropUrl=null),new Promise(function(resolve,reject){var dlg=document.querySelector(".videoPlayerContainer");dlg?(options.backdropUrl&&(dlg.classList.add("videoPlayerContainer-withBackdrop"),dlg.style.backgroundImage="url('"+options.backdropUrl+"')"),resolve(dlg.querySelector("video"))):require(["css!./style"],function(){loading.show();var dlg=document.createElement("div");dlg.classList.add("videoPlayerContainer"),options.backdropUrl&&(dlg.classList.add("videoPlayerContainer-withBackdrop"),dlg.style.backgroundImage="url('"+options.backdropUrl+"')"),options.fullscreen&&dlg.classList.add("videoPlayerContainer-onTop");var html="",cssClass="htmlvideoplayer";browser.chromecast||(cssClass+=" htmlvideoplayer-moveupsubtitles"),html+=appHost.supports("htmlvideoautoplay")?'<video class="'+cssClass+'" preload="metadata" autoplay="autoplay" webkit-playsinline playsinline>':'<video class="'+cssClass+'" preload="metadata" autoplay="autoplay" controls="controls" webkit-playsinline playsinline>',html+="</video>",dlg.innerHTML=html;var videoElement=dlg.querySelector("video");videoElement.volume=htmlMediaHelper.getSavedVolume(),videoElement.addEventListener("timeupdate",onTimeUpdate),videoElement.addEventListener("ended",onEnded),videoElement.addEventListener("volumechange",onVolumeChange),videoElement.addEventListener("pause",onPause),videoElement.addEventListener("playing",onPlaying),videoElement.addEventListener("play",onPlay),videoElement.addEventListener("click",onClick),videoElement.addEventListener("dblclick",onDblClick),document.body.insertBefore(dlg,document.body.firstChild),videoDialog=dlg,self._mediaElement=videoElement,options.fullscreen&&browser.supportsCssAnimation()&&!browser.slow?zoomIn(dlg).then(function(){resolve(videoElement)}):resolve(videoElement)})})}browser.edgeUwp?this.name="Windows Video Player":this.name="Html Video Player",this.type="mediaplayer",this.id="htmlvideoplayer",this.priority=1;var videoDialog,subtitleTrackIndexToSetOnPlaying,currentClock,currentAssRenderer,videoSubtitlesElem,currentTrackEvents,lastCustomTrackMs=0,customTrackIndex=-1,self=this;self.currentSrc=function(){return self._currentSrc},self.play=function(options){return browser.msie&&"Transcode"===options.playMethod&&!window.MediaSource?(alert("Playback of this content is not supported in Internet Explorer. For a better experience, try a modern browser such as Microsoft Edge, Google Chrome, Firefox or Opera."),Promise.reject()):(self._started=!1,self._timeUpdated=!1,self._currentTime=null,createMediaElement(options).then(function(elem){return updateVideoUrl(options,options.mediaSource).then(function(){return setCurrentSrc(elem,options)})}))},self.setSubtitleStreamIndex=function(index){setCurrentTrackElement(index)},self.setAudioStreamIndex=function(index){var i,length,audioStreams=getMediaStreamAudioTracks(self._currentPlayOptions.mediaSource),audioTrackOffset=-1;for(i=0,length=audioStreams.length;i<length;i++)if(audioStreams[i].Index===index){audioTrackOffset=i;break}if(audioTrackOffset!==-1){var elem=self._mediaElement;if(elem){var elemAudioTracks=elem.audioTracks||[];for(i=0,length=elemAudioTracks.length;i<length;i++)audioTrackOffset===i?elemAudioTracks[i].enabled=!0:elemAudioTracks[i].enabled=!1}}},self.stop=function(destroyPlayer){var elem=self._mediaElement,src=self._currentSrc;return elem&&(src&&elem.pause(),htmlMediaHelper.onEndedInternal(self,elem,onError),destroyPlayer&&self.destroy()),destroyCustomTrack(elem),Promise.resolve()},self.destroy=function(){htmlMediaHelper.destroyHlsPlayer(self),embyRouter.setTransparency("none");var videoElement=self._mediaElement;videoElement&&(self._mediaElement=null,destroyCustomTrack(videoElement),videoElement.removeEventListener("timeupdate",onTimeUpdate),videoElement.removeEventListener("ended",onEnded),videoElement.removeEventListener("volumechange",onVolumeChange),videoElement.removeEventListener("pause",onPause),videoElement.removeEventListener("playing",onPlaying),videoElement.removeEventListener("play",onPlay),videoElement.removeEventListener("loadedmetadata",onLoadedMetadata),videoElement.removeEventListener("click",onClick),videoElement.removeEventListener("dblclick",onDblClick),videoElement.parentNode.removeChild(videoElement));var dlg=videoDialog;dlg&&(videoDialog=null,dlg.parentNode.removeChild(dlg))},self.destroyCustomTrack=destroyCustomTrack}function getSupportedFeatures(){var list=[],video=document.createElement("video");return browser.ipad&&navigator.userAgent.toLowerCase().indexOf("os 9")===-1&&video.webkitSupportsPresentationMode&&video.webkitSupportsPresentationMode&&"function"==typeof video.webkitSetPresentationMode&&list.push("PictureInPicture"),list.push("SetBrightness"),list}HtmlVideoPlayer.prototype.canPlayMediaType=function(mediaType){return"video"===(mediaType||"").toLowerCase()},HtmlVideoPlayer.prototype.supportsPlayMethod=function(playMethod,item){return!appHost.supportsPlayMethod||appHost.supportsPlayMethod(playMethod,item)},HtmlVideoPlayer.prototype.getDeviceProfile=function(item,options){return appHost.getDeviceProfile?appHost.getDeviceProfile(item,options):getDefaultProfile()};var supportedFeatures;return HtmlVideoPlayer.prototype.supports=function(feature){return supportedFeatures||(supportedFeatures=getSupportedFeatures()),supportedFeatures.indexOf(feature)!==-1},HtmlVideoPlayer.prototype.currentTime=function(val){var mediaElement=this._mediaElement;if(mediaElement){if(null!=val)return void(mediaElement.currentTime=val/1e3);var currentTime=this._currentTime;return currentTime?1e3*currentTime:1e3*(mediaElement.currentTime||0)}},HtmlVideoPlayer.prototype.duration=function(val){var mediaElement=this._mediaElement;if(mediaElement){var duration=mediaElement.duration;if(htmlMediaHelper.isValidDuration(duration))return 1e3*duration}return null},HtmlVideoPlayer.prototype.canSetAudioStreamIndex=function(){return!(!browser.edge&&!browser.msie)},HtmlVideoPlayer.prototype.setPictureInPictureEnabled=function(isEnabled){var video=this._mediaElement;video&&video.webkitSupportsPresentationMode&&"function"==typeof video.webkitSetPresentationMode&&video.webkitSetPresentationMode(isEnabled?"picture-in-picture":"inline")},HtmlVideoPlayer.prototype.isPictureInPictureEnabled=function(){var video=this._mediaElement;return!!video&&"picture-in-picture"===video.webkitPresentationMode},HtmlVideoPlayer.prototype.setBrightness=function(val){var elem=this._mediaElement;if(elem){val=Math.max(0,val),val=Math.min(100,val);var rawValue=val;rawValue=Math.max(20,rawValue);var cssValue=rawValue>=100?"none":rawValue/100;elem.style["-webkit-filter"]="brightness("+cssValue+");",elem.style.filter="brightness("+cssValue+")",elem.brightnessValue=val,events.trigger(this,"brightnesschange")}},HtmlVideoPlayer.prototype.getBrightness=function(){var elem=this._mediaElement;if(elem){var val=elem.brightnessValue;return null==val?100:val}},HtmlVideoPlayer.prototype.seekable=function(){var mediaElement=this._mediaElement;if(mediaElement){var seekable=mediaElement.seekable;if(seekable&&seekable.length){var start=seekable.start(0),end=seekable.end(0);return htmlMediaHelper.isValidDuration(start)||(start=0),htmlMediaHelper.isValidDuration(end)||(end=0),end-start>0}return!1}},HtmlVideoPlayer.prototype.pause=function(){var mediaElement=this._mediaElement;mediaElement&&mediaElement.pause()},HtmlVideoPlayer.prototype.resume=function(){var mediaElement=this._mediaElement;mediaElement&&mediaElement.play()},HtmlVideoPlayer.prototype.unpause=function(){var mediaElement=this._mediaElement;mediaElement&&mediaElement.play()},HtmlVideoPlayer.prototype.paused=function(){var mediaElement=this._mediaElement;return!!mediaElement&&mediaElement.paused},HtmlVideoPlayer.prototype.setVolume=function(val){var mediaElement=this._mediaElement;mediaElement&&(mediaElement.volume=val/100)},HtmlVideoPlayer.prototype.getVolume=function(){var mediaElement=this._mediaElement;if(mediaElement)return 100*mediaElement.volume},HtmlVideoPlayer.prototype.volumeUp=function(){this.setVolume(Math.min(this.getVolume()+2,100))},HtmlVideoPlayer.prototype.volumeDown=function(){this.setVolume(Math.max(this.getVolume()-2,0))},HtmlVideoPlayer.prototype.setMute=function(mute){var mediaElement=this._mediaElement;mediaElement&&(mediaElement.muted=mute)},HtmlVideoPlayer.prototype.isMuted=function(){var mediaElement=this._mediaElement;return!!mediaElement&&mediaElement.muted},HtmlVideoPlayer.prototype.setAspectRatio=function(val){},HtmlVideoPlayer.prototype.getAspectRatio=function(){return this._currentAspectRatio},HtmlVideoPlayer.prototype.getSupportedAspectRatios=function(){return[]},HtmlVideoPlayer.prototype.togglePictureInPicture=function(){return this.setPictureInPictureEnabled(!this.isPictureInPictureEnabled())},HtmlVideoPlayer.prototype.getStats=function(){var mediaElement=this._mediaElement,playOptions=this._currentPlayOptions||[];if(!mediaElement)return Promise.resolve([]);var categories=[],mediaCategory={stats:[],type:"media"};if(categories.push(mediaCategory),playOptions.url){var link=document.createElement("a");link.setAttribute("href",playOptions.url);var protocol=(link.protocol||"").replace(":","");protocol&&mediaCategory.stats.push({label:"Protocol:",value:protocol}),link=null}this._hlsPlayer?mediaCategory.stats.push({label:"Stream type:",value:"HLS"}):mediaCategory.stats.push({label:"Stream type:",value:"Video"}),playOptions.mimeType&&mediaCategory.stats.push({label:"Mime type:",value:playOptions.mimeType});var videoCategory={stats:[],type:"video"};categories.push(videoCategory);var rect=mediaElement.getBoundingClientRect?mediaElement.getBoundingClientRect():{},height=rect.height,width=rect.width;if(width&&height&&videoCategory.stats.push({label:"Player dimensions:",value:width+"x"+height}),height=mediaElement.videoHeight,width=mediaElement.videoWidth,width&&height&&videoCategory.stats.push({label:"Video resolution:",value:width+"x"+height}),mediaElement.getVideoPlaybackQuality){var playbackQuality=mediaElement.getVideoPlaybackQuality(),droppedVideoFrames=playbackQuality.droppedVideoFrames||0;videoCategory.stats.push({label:"Dropped frames:",value:droppedVideoFrames});var corruptedVideoFrames=playbackQuality.corruptedVideoFrames||0;videoCategory.stats.push({label:"Corrupted frames:",value:corruptedVideoFrames})}var audioCategory={stats:[],type:"audio"};categories.push(audioCategory);for(var audioTrackList=mediaElement.audioTracks||[],audioTracks=[],i=0,length=audioTrackList.length;i<length;i++){var audioTrack=audioTrackList[i];audioTrack.enabled?audioTracks.push((audioTrack.language||"und")+" (enabled)"):audioTracks.push(audioTrack.language||"und")}audioTracks.length&&audioCategory.stats.push({label:"Audio tracks:",value:audioTracks.join("<br/>")});var sinkId=mediaElement.sinkId;return sinkId&&audioCategory.stats.push({label:"Sink Id:",value:sinkId}),Promise.resolve(categories)},HtmlVideoPlayer});