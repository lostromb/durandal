// usage with this HTML element: <audio src="data:audio/wav;base64,UklGRpoiAABXQVZFZm==" id="AudioElement"></audio>

// variables
var leftchannel = [];
var rightchannel = [];
var recorder = null;
var recording = false;
var recordingLength = 0;
var volume = null;
var audioInput = null;
var sampleRate = null;
var audioContext = null;
var context = null;
var outputString;
var playing = null;
var audio = document.getElementById('AudioElement') || new Audio();
var beep = 'data:audio/wav;base64,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'
var colors = {};

var currentState = 'ready';
var currentClass;
var oldState;
var TM = TweenMax;


var multidata;
var audiopart;

var mydata;
var myresp;
var currentDialogID;


colors.ready='#ffffff';
colors.listening='#00ffff';
colors.processing='#0000ff';
colors.speaking='green';
colors.error='red';
var checker;
// feature detection 

var UserMediaAudioSupport=true;

if (!navigator.getUserMedia)
    navigator.getUserMedia = navigator.getUserMedia || navigator.webkitGetUserMedia ||
                  navigator.mozGetUserMedia || navigator.msGetUserMedia;

function setState(state){

  oldState = currentState;
  currentClass = 'state-'+state;
  currentRing = 'ring-'+state;
  currentState = state;

  $('.ring-speaking, .ring-processing, .ring-ready').css({visibility:'hidden'});


  console.log('Status: '+currentState.toUpperCase() );

  if(currentState == 'listening'){
    $('.ring-slide').css({visibility:'visible'});
  }

  $('.'+currentRing).css({visibility:'visible'});

  if(oldState == 'speaking' || oldState == 'error'){
    stateTimeline.seek('sleep').play();
  }
  else{
    stateTimeline.seek(currentState).play();
  }

  switch(state) {
      case 'listening':
          pulse.play();
          break;
      case 'ready':
          pulse.seek(0.4).tweenTo('start');
          break;
      default:
          pulse.play();
  }


  $('#mic, .echo-btnbg').removeClass('state-'+oldState);
  $('#mic, .echo-btnbg').addClass('state-'+currentState);

  $path = '/static/assets/img/state/';


  TweenMax.fromTo('.state-text h4',0.15,{opacity:1,x:0},{ x:6,opacity:0,yoyo:true,repeat:1,onComplete:setStateText,onCompleteParams:[state] })



}

function setStateText(st){
  if (st == 'error'){
    $('.state-text h4').text('Try Again...');
  }
  else{
    if(UserMediaAudioSupport){
      $('.state-text h4').text(st+'...');
    }
    else{
       $('.state-text h4').html('Audio input not available.');
    }
    

  }
}



function setup(){
	if (navigator.getUserMedia){
	    navigator.getUserMedia({audio:true}, success, function(e) {
	    alert('Error capturing audio.');
	    });
	} else {
    var aesupport_opts = {};
    UserMediaAudioSupport = false;
    $('#ae-support').modal(aesupport_opts)
    console.log('getUserMedia not supported in this browser. Please refer to http://echosim.io/help for available options.');
  }

}


function startrecord(){
		audio.src= beep
		playing = "beep";
		audio.play();
		setState('listening');
		recording = true;
        leftchannel.length = rightchannel.length = 0;
        recordingLength = 0;
} 

// var resp;
// var recorded;


function stoprecord(){
		recording = false;
        setState('processing');
        var leftBuffer = mergeBuffers ( leftchannel, recordingLength );
        var rightBuffer = mergeBuffers ( rightchannel, recordingLength );
        var interleaved = interleave ( leftBuffer, rightBuffer );
        
        var buffer = new ArrayBuffer(44 + interleaved.length * 2);
        var view = new DataView(buffer);
        
        writeUTFBytes(view, 0, 'RIFF');
        view.setUint32(4, 44 + interleaved.length * 2, true);
        writeUTFBytes(view, 8, 'WAVE');
        writeUTFBytes(view, 12, 'fmt ');
        view.setUint32(16, 16, true);
        view.setUint16(20, 1, true);
        view.setUint16(22, 2, true);
        view.setUint32(24, sampleRate, true);
        view.setUint32(28, sampleRate * 4, true);
        view.setUint16(32, 4, true);
        view.setUint16(34, 16, true);
        writeUTFBytes(view, 36, 'data');
        view.setUint32(40, interleaved.length * 2, true);
        
        var lng = interleaved.length;
        var index = 44;
        var volume = 1;
        for (var i = 0; i < lng; i++){
            view.setInt16(index, interleaved[i] * (0x7FFF * volume), true);
            index += 2;
        }
        
        var blob = new Blob([view], { type: 'audio/wav' });
        var fd = new FormData();
        fd.append('fname', 'audio.wav');
        fd.append('data', blob);




        // callNew(fd);




        var xhr = new XMLHttpRequest();
        xhr.multipart = true;
        xhr.previous_text = '';
        xhr.onreadystatechange = checkContentType;

        xhr.open('POST', '/audio', true);
        // xhr.responseType = "arraybuffer";
        // xhr.responseType = "json";
        // xhr.responseType = "";
        // xhr.responseType = "";
        xhr.responseType = "blob";


        xhr.onload = function(evt) {

          if(this.status == 200){


                // console.log('RESPONSE:'+this.response);


                cnt_type = this.getResponseHeader('Content-Type');
                // console.log('CNT_TYPE:' + cnt_type);
                // console.log('RESPONSE_TYPE:' + this.responseType);

                if (cnt_type == 'audio/mpeg' && this.responseType == 'blob' ) {

                    theboundary = cnt_type.replace(/.*boundary=([^\s;]+).*/, '$1');
                    // console.log('INCOMING: (' + theboundary + ')');

                    var blob = new Blob([this.response], { type: 'audio/mpeg' });
                    var objectUrl = URL.createObjectURL(blob);
                    audio.addEventListener('ended', function() {
                        if (playing == 'response') {
                            setState('ready');
                        }
                        playing = null;
                    });
                    audio.src = objectUrl;
                    audio.onload = function(evt) {
                        URL.revokeObjectUrl(objectUrl);
                    };
                    setState('speaking');
                    audio.play();
                    playing = "response";

                }
                else{
                    if(this.responseType == 'blob'){
                      console.log('BLOB: '+this.getAllResponseHeaders());
                      
                    }
                    if(this.responseType == 'text'){
                      console.log('TEXT:' + this.getAllResponseHeaders());
                    }
                    setState('error');
                }



          }
          else{
            //set error state
            setState('error');
          }
          
        }


        xhr.send(fd);

		
    }
function checkContentType() {
    // try {
    //     if (this.readyState > 2) {

    //     }
    // } catch (e) {
    //     console.log("XHR Exception: " + e);
    // }


    if (this.readyState == 2) {
        if (this.getResponseHeader('Content-Type') == 'audio/mpeg') {
            this.responseType = 'blob'
        }
        else if (this.getResponseHeader('Content-Type') == 'application/json') {
            this.responseType = 'json'            
        }
        else if (this.getResponseHeader('Content-Type') == 'text/html') {
            this.responseType = 'text'
        }
    }
}

function consumeAudioQueue(){
  //check until last audio has played
  //notify all audio has been played to Alexa, we are ready for more!
}


function parseMultipartAudio(part)
{
  //add new audio to the queue
}

function interleave(leftChannel, rightChannel){
  var length = leftChannel.length + rightChannel.length;
  var result = new Float32Array(length);

  var inputIndex = 0;

  for (var index = 0; index < length; ){
    result[index++] = leftChannel[inputIndex];
    result[index++] = rightChannel[inputIndex];
    inputIndex++;
  }
  return result;
}




function mergeBuffers(channelBuffer, recordingLength){
  var result = new Float32Array(recordingLength);
  var offset = 0;
  var lng = channelBuffer.length;
  for (var i = 0; i < lng; i++){
    var buffer = channelBuffer[i];
    result.set(buffer, offset);
    offset += buffer.length;
  }
  return result;
}

function writeUTFBytes(view, offset, string){ 
  var lng = string.length;
  for (var i = 0; i < lng; i++){
    view.setUint8(offset + i, string.charCodeAt(i));
  }
}

function success(e){
    audioContext = window.AudioContext || window.webkitAudioContext;
    context = new audioContext();

    sampleRate = context.sampleRate;

    console.log('succcess');
    volume = context.createGain();
    audioInput = context.createMediaStreamSource(e);
    audioInput.connect(volume);

    var bufferSize = 2048;
    recorder = context.createScriptProcessor(bufferSize, 2, 2);

    recorder.onaudioprocess = function(e){
        if (!recording) return;
        var left = e.inputBuffer.getChannelData (0);
        var right = e.inputBuffer.getChannelData (1);
        leftchannel.push (new Float32Array (left));
        rightchannel.push (new Float32Array (right));
        recordingLength += bufferSize;
    }

    volume.connect (recorder);
    recorder.connect (context.destination); 
	setState('ready');
}
