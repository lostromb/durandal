// This class is used by functions which run inside of answer HTML pages (in other words, they are stuck inside the client's container)
// Functions here will assist in communicating with the external container and passing messages.
// This class does not function as a client to make arbitrary requests.
var DurandalAssistant = function() {
	var that = {};
	
	// Gather some info about our environment
	var isInsideBrowser = typeof console !== "undefined" && typeof window !== "undefined";
	var isInsideIFrame = isInsideBrowser && window.frameElement !== null && typeof window.frameElement.tagName !== "undefined" && window.frameElement.tagName === "IFRAME";
	var isInsideXaml = typeof window !== "undefined" && typeof window.external !== "undefined" && window.external.IsDurandal !== undefined && window.external.IsDurandal();
	var isInsideLocalHttp = true;
	
	function jsEnabledAjaxListener (evt) {
		isInsideLocalHttp = this.status == 200
	}

	var localHttpProbeRequest = new XMLHttpRequest();
	localHttpProbeRequest.addEventListener("load", jsEnabledAjaxListener);
	localHttpProbeRequest.open("GET", "/js/enabled");
	localHttpProbeRequest.setRequestHeader("Content-Type", "text/plain;charset=UTF-8");
	localHttpProbeRequest.overrideMimeType("text/plain;charset=UTF-8");
	localHttpProbeRequest.send();
	
	var currentRequestData = {};
	var currentLogs = [];
	
	var logInternal = function(message) {
		if (isInsideBrowser) {
			console.log(message);
		}
		
		if (isInsideXaml) {
			// pass log messages to the native client to be logged
			window.external.LogMessage(message);
		}
		
		if (isInsideLocalHttp)
		{
			var ajaxRequest = new XMLHttpRequest();
			ajaxRequest.open("POST", "/js/log");
			ajaxRequest.setRequestHeader("Content-Type", "text/plain;charset=UTF-8");
			ajaxRequest.overrideMimeType("text/plain;charset=UTF-8");
			ajaxRequest.send(message);
		}
		
		currentLogs.push(message);
	}
	
	that.updateRequestData = function(key, val) {
		logInternal("Client javascript has updated request data: Key = " + key + ", Value = " + val);
		
		// This route fires if we are in an IFrame
		if (isInsideIFrame) {
			var clientData = JSON.parse(window.frameElement.innerHTML);
			clientData.RequestData[key] = val;
			window.frameElement.innerHTML = JSON.stringify(clientData);
			// logInternal("The request was fulfilled via IFrame data");
		}
		
		// This route fires if we are within a XAML WebBrowser element
		if (isInsideXaml) {
			window.external.UpdateRequestData(key, val);
			// logInternal("The request was fulfilled via an external scripting object");
		}
		
		// This route fires if we are running on a locally-hosted client HTTP server
		if (isInsideLocalHttp)
		{
			var ajaxRequest = new XMLHttpRequest();
			ajaxRequest.open("POST", "/js/data");
			ajaxRequest.setRequestHeader("Content-Type", "application/x-www-form-urlencoded");
			ajaxRequest.overrideMimeType("application/x-www-form-urlencoded");
			ajaxRequest.send(escape(key) + "=" + escape(val));
		}
		
		currentRequestData[key] = val;
	}
	
	that.logMessage = function(message) {
		logInternal(message);
	}
	
	// these functions are exposed for "degenerate" clients that can't provide a scripting object.
	// In these cases, the best we can hope for is that the client will manually invoke these functions
	// to proactively pull the data it needs
	that.getRequestData = function() {
		return JSON.stringify(currentRequestData);
	}
	
	that.getLogs = function() {
		return JSON.stringify(currentLogs);
	}
	
	return that;
}();