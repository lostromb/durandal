var Timer = function() {
	var that = {};
	var countsDown = true;
	// This stores the absolute Unix time of when the timer started or when it is scheduled to stop
	// This is set by the dialog logic so as to be as accurate as possible
	var targetTimeEpoch = 0;
	var msOnTimer = 0;
	var currentTime = new Date();
	var timerPaused = false;
	var timerRunning = false;
	var timerElapsed = false;
	
	var formatNumberTwo = function(number) {
		var returnVal = "";
		if (number < 10)
		{
			returnVal += "0";
		}
		return returnVal + number;
	}
	
	var formatNumberThree = function(number) {
		var returnVal = "";
		if (number < 100)
		{
			returnVal += "0";
		}
	
		if (number < 10)
		{
			returnVal += "0";
		}
		return returnVal + number;
	}
	
	var updateClientState = function() {
		if (DurandalAssistant) {
			DurandalAssistant.updateRequestData("timer_targetTimeEpoch", targetTimeEpoch);
			DurandalAssistant.updateRequestData("timer_msOnTimer", msOnTimer);
			DurandalAssistant.updateRequestData("timer_paused", timerPaused);
			DurandalAssistant.updateRequestData("timer_running", timerRunning);
			DurandalAssistant.updateRequestData("timer_elapsed", timerElapsed);
			DurandalAssistant.updateRequestData("timer_countsDown", countsDown);
		}
		else
		{
			console.log("durandal_assistant.js not imported!");
		}
	}
	
	var incrementCountdown = function() {
		if (!timerPaused && timerRunning) {
			msOnTimer = new Date().getTime()- targetTimeEpoch;	
			
			if (countsDown) {
				msOnTimer = 0 - msOnTimer;
			}
			
			if (msOnTimer < 0) {
				msOnTimer = 0;
				if (countsDown) {
					// Countdown timer has elapsed, set the flag
					timerRunning = false;
					timerElapsed = true;
				}
			}
			
			currentTime.setTime(msOnTimer);
			document.getElementById("hoursField").innerHTML = formatNumberTwo(currentTime.getUTCHours());
			document.getElementById("minutesField").innerHTML = formatNumberTwo(currentTime.getUTCMinutes());
			document.getElementById("secondsField").innerHTML = formatNumberTwo(currentTime.getUTCSeconds());
			document.getElementById("msField").innerHTML = formatNumberThree(currentTime.getUTCMilliseconds());
		}
	}
	
	that.start = function(isDown, targetTime) {
		if (!timerRunning) {
			countsDown = isDown;
			targetTimeEpoch = targetTime;
			setInterval(incrementCountdown, 93);
			timerRunning = true;
		}
	}
	
	that.pause = function() {
		if (timerPaused)
		{
			// Unpause
			if (countsDown) {
				targetTimeEpoch = new Date().getTime() + msOnTimer;
			}
			else {
				targetTimeEpoch = new Date().getTime() - msOnTimer;
			}
			
			timerPaused = false;
			document.getElementById("pauseButton").innerHTML = "Pause";
		}
		else
		{
			// Pause
			msOnTimer = new Date().getTime()- targetTimeEpoch ;	
			
			if (countsDown) {
				msOnTimer = 0 - msOnTimer;
			}
			
			if (msOnTimer < 0) {
				msOnTimer = 0;
			}
	
			timerPaused = true;
			document.getElementById("pauseButton").innerHTML = "Unpause";
		}
		updateClientState();
	}
	
	that.stop = function() {
		timerRunning = false;
		updateClientState();
	}
	
	return that;
}();
