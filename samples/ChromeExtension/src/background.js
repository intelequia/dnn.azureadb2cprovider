chrome.runtime.onInstalled.addListener(function() {

    // Only enable the addon when browsing a specific website
    chrome.declarativeContent.onPageChanged.removeRules(undefined, function() {
        chrome.declarativeContent.onPageChanged.addRules([{
            conditions: [
                new chrome.declarativeContent.PageStateMatcher({
                    pageUrl: {hostEquals: 'intelequia.com'},
                }),
                new chrome.declarativeContent.PageStateMatcher({
                    pageUrl: {hostEquals: 'github.com'},
                })
            ],
            actions: [new chrome.declarativeContent.ShowPageAction()]
        }]);
    });    
});
 
var version = "1.0";
