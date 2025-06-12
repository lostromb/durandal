var c = document.getElementById("InteractionTrigger");
var ctx = c.getContext("2d");
var circle = new Path2D();
var btn;
var els, tl, pulse, ring, wake;

function layout() {
    
    var $curv1 = $("path#CURVE_LEFT");
    var $curv2 = $("path#CURVE_RIGHT");

    // prepare SVG
    pathPrepare($curv1);
    pathPrepare($curv2);

    stateTimeline = new TimelineMax();
    TM.set('.ring-speaking, .ring-processing, .wake, .ring-ready', {rotationX:75.7,transformOrigin:'50% 50%'})

    TM.set('.rings',{scale:1.03});
    TM.set('.r_left', {rotation:-90});
    TM.set('.r_right', {rotation:-90});
    TM.set('.ring-slide',{rotation:180});
    
    TM.set('.ring-slide',{visibility:'hidden'});
    TM.set('.ring-listening',{visibility:'hidden'});

    stateTimeline

        .addPause()
    
        .addLabel('ready')
        .set('.ring-ready', {scale:1})
        .addPause()

        .addLabel('listening')
        .to($curv1, 0.65, {strokeDashoffset: 0, ease:Sine.easeInOut})
        .to($curv2, 0.65, {strokeDashoffset: 0, ease:Sine.easeInOut},0)
        .to('.ring-listening .r_left', 0.7, {rotation:'+=180', ease:Sine.easeInOut},'listening')
        .to('.ring-listening .r_right', 0.7, {rotation:'-=180', ease:Sine.easeInOut},'listening')
        .addPause()

        .addLabel('processing')
        .to('.ring-processing img',1,{yoyo:false,repeat:999,rotation:360, ease:Linear.easeNone })
        .addPause()

        .addLabel('speaking')
        .to('.ring-speaking-top',0.25,{yoyo:true, repeat:999,opacity:0, ease:Linear.easeNone})
        .addPause()

        .addLabel('sleep')
        .to('.ring-listening .r_left', 0.7, {rotation:'-=180', ease:Sine.easeInOut},'sleep')
        .to('.ring-listening .r_right', 0.7, {rotation:'+=180', ease:Sine.easeInOut},'sleep')        
        .addLabel('slideout')
        .to($curv1, 0.65, {strokeDashoffset: $curv1.css("stroke-dasharray"), ease:Sine.easeInOut},'slideout-=0.2')
        .to($curv2, 0.65, {strokeDashoffset: $curv2.css("stroke-dasharray"), ease:Sine.easeInOut},'slideout-=0.2')
        .to('.wake',0.2,{opacity:0},'slideout-=0.05')
        .addPause();

        
    TM.set('.rings-wrapper', {visibility:'visible'});


if (UserMediaAudioSupport) {
    if (is_touch_device()) {
        var el = document.getElementById("touch");
        c.ontouchstart = function(e) {
            startrecord();
        }
        c.ontouchend = function(e) {
            stoprecord();
        }
    } else {
        c.onmousedown = function(e) {
            startrecord();
        }
        c.onmouseup = function(e) {
            stoprecord();
        }
    }    
}
else{
    setState('ready');
}
}



function pathPrepare ($el) {
        var lineLength = $el[0].getTotalLength();
        $el.css("stroke-dasharray", lineLength);
        $el.css("stroke-dashoffset", lineLength);
}


function bindButton(){

    btn = $('#mic');
    triggerOb = $('#InteractionTrigger');
    btn.on('mousedown mouseup touchstart touchend',function(e){
        if ( !(e.type == 'mousedown' && e.button !=0) ){
                triggerOb.trigger(e.type);
            }
    });


}


function log(data) {
    console.log('logging...')
    console.log(data);
}

function is_touch_device() {
    return !!('ontouchstart' in window);
}


window.onload = function() {
    setup();
    console.log('AUDIO INPUT SUPPORT? : '+ UserMediaAudioSupport);
    layout();
    bindButton();
};

window.onresize = function() {
    // location.reload();
};

window.oncontextmenu = function(event) {
     // event.preventDefault();
     // event.stopPropagation();
     // return false;
};

$(document).ready(function() {

$(".full-echo, echo-btn").on("contextmenu",function(){
       return false;
}); 

    $('.notSelectable').disableSelection();
    els = $('.echo-btnbg');
    pulse = new TimelineMax({ paused: true });  
    pulse.staggerTo(els, 0.8, {scale:1.4, transformOrigin:"50% 50%", yoyo:true, repeat:-1}, 0.2);
    pulse.addLabel('start',0);

});

// This jQuery Plugin will disable text selection for Android and iOS devices.
// Stackoverflow Answer: http://stackoverflow.com/a/2723677/1195891
$.fn.extend({
    disableSelection: function() {
        this.each(function() {
            this.onselectstart = function() {
                return false;
            };
            this.unselectable = "on";
            $(this).css('-moz-user-select', 'none');
            $(this).css('-webkit-user-select', 'none');
        });
    }
});