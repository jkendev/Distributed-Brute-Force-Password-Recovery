<%@ Page Language="C#" AutoEventWireup="true" CodeFile="PWDRecoveryViewer.aspx.cs" Inherits="PWDRecoveryViewer" %>

<!DOCTYPE html>

<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <title>Distributed Brute Force Password Cracker - Web Page</title>
    <script>

        /***************************** JAVA SCRIPT *******************************************/
        //Begins the recovery process, using ajax call to web service.
        function BeginRecovery()  //Slide 32 of ajax lecture
        {
            document.getElementById("btnBeginRecovery").disabled = true;
            BeginRecoveryAsync(BeginRecovery_OnComplete);
        }

        //Asynchronous call for beginning the recovery
        function BeginRecoveryAsync(fnOnComplete)
        {
            req = null;
            if (window.XMLHttpRequest != undefined) {
                req = new XMLHttpRequest();
            }
            else {
                req = new ActiveXObject("Microsoft.XMLHTTP");
            }

            req.onreadystatechange = fnOnComplete;
            req.open("POST", "PWDRecoveryWebService.svc", true);

            req.setRequestHeader("Content-Type", "text/xml");
            req.setRequestHeader("SOAPAction", "http://tempuri.org/IPWDRecoveryWebService/wsBeginRecovery");
            
            

            var sMsg = '<?xml version="1.0" encoding="utf-8"?> \
                        <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">\
                            <soap:Body>\
                                <wsBeginRecovery xmlns="http://tempuri.org/" />\
                            </soap:Body>\
                        </soap:Envelope>'

            req.send(sMsg);

        }

        //The callback function for begin recovery.
        function BeginRecovery_OnComplete()
        {
            {
                if (req.readyState == 4) {

                    if (req.status == 200) {
                        //alert(req.responseText);
                        var ndResult = req.responseXML.documentElement.getElementsByTagName("wsBeginRecoveryResult");   
                        
                        
                        document.getElementById('pPassword').innerText = ndResult[0].childNodes[0].nodeValue;

                        document.getElementById("btnBeginRecovery").disabled = false;

                        //alert(ndResult[0].childNodes[0].nodeValue);
                    }
                    else {
                        alert("Asynchronous call failed. ResponseText was:\n" + req.responseText);
                        alert("Req.status:" + req.status);
                    }
                    req = null;
                }
            }
        }

       //Begins the random password generation call.
        function GenerateRandom()
        {
            var length = String(document.getElementById("txtLength").value);
            //lengthStr.replace(/[^0-9]/g, "");  //Get rid of anything not a digit character.

            //var iLength = parseInt(lengthStr);

            if (isNaN(length))
            {
                alert("Length provided is not a number");

            }
            else
            {
                var iLength = parseInt(length);
                if (isNaN(iLength))
                {
                    alert("Invalid length. Please enter a number between between 1 and 10");
                }
                if (iLength <= 0 || length > 10)
                {
                    alert("Length provided must be between 1 and 10 inclusive");
                }
                else
                {
                    document.getElementById('txtLength').value = iLength;   //update the text box so that it has the truncated value (eg if someone puts 3.22, it becomes just 3). Feedback is important!

                    //Call async function:
                    GenerateRandomRPCAsync(iLength, GenerateRandomRPC_OnComplete);
                    
                }                
            }
        }

        //The ajax call to the web server to generate the random encrypted password of given length
        function GenerateRandomRPCAsync(iLength, fnOnComplete)
        {
            req = null;
            if (window.XMLHttpRequest != undefined) {
                req = new XMLHttpRequest();
            }
            else {
                req = new ActiveXObject("Microsoft.XMLHTTP");
            }

            req.onreadystatechange = fnOnComplete;
            req.open("POST", "PWDRecoveryWebService.svc", true);

            req.setRequestHeader("Content-Type", "text/xml");
            req.setRequestHeader("SOAPAction", "http://tempuri.org/IPWDRecoveryWebService/wsGenerateLength");

            var sMsg = '<?xml version="1.0" encoding="utf-8"?> \
                       <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">\
                            <soap:Body>\
                                <wsGenerateLength xmlns="http://tempuri.org/">\
                                    <length>'+iLength+'</length>\
                                </wsGenerateLength>\
                            </soap:Body>\
                        </soap:Envelope>';

            req.send(sMsg);
        }

        //The callback function when the random password has been encrypted and returned.
        function GenerateRandomRPC_OnComplete()
        {
            if (req.readyState == 4) {

                if (req.status == 200) {
                    //alert(req.responseText);
                    var ndResult = req.responseXML.documentElement.getElementsByTagName("wsGenerateLengthResult");   //param is from the xml return in the test program!
                    document.getElementById('pEncryptedPW').innerText = "Encrypted password to recover: " + ndResult[0].childNodes[0].nodeValue;
                    //ndResult[0].childNodes[0].nodeValue;
                    //alert(ndResult[0].childNodes[0].nodeValue);
                }
                else {
                    alert("Asynchronous call failed. ResponseText was:\n" + req.responseText);
                    alert("Req.status:" + req.status);
                }
                req = null;
            }
        }

    </script>
</head>
<body>
    <form id="frmMain" runat="server">
    <div>
    
        <asp:Label ID="lblTitle" runat="server" Font-Bold="True" Font-Size="Larger" Text="Distributed Brute Force Password Cracker v1.0"></asp:Label>
        

    </div>
    <div>
        Password Length: <input type="text" id ="txtLength" runat="server"/>
        
        <input id="btnSubmitLength" type="button" value="Generate Random" onclick="GenerateRandom()" />
    </div>

    <div>
        <p id ="pEncryptedPW">Encrypted Password:</p>
        
    </div>
    <div>
        <input id="btnBeginRecovery" type="button" value="Begin Recovery" onclick="BeginRecovery()" />
        <p id ="pPassword"></p>
    </div>
    </form>
</body>
</html>
