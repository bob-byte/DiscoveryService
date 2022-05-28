# LightClientUpload

This is a library for uploading files to RIAK servers. Can used by this parameters:

var lightClient = new LightClient.LightClient();  
HttpResponseMessage response = await lightClient.Upload(string host, string token, string user_id, string bucket_id, string fullPath, string filePrefix, string guid = "");  

        Host - server Url. Example - https://lightupon.cloud
        Token - authorization token from server. Example - "647c7fde-936c-447a-8640-55dc8c1c69cb"
        User_id - identificator from server. Example - "03a3a647d7e65013f515b16b1d9225b6"
        bucket_id - bucket from server. Example - "the-integrationtests-integration1-res"
        fullPath - full path to the file
        filePrefix - prefix from server, need if file located in the subdirectory, else ""
        lastseenversion - vector clock version from server (optional parameter)

Method contains a vector version clock (DVVSet), so we can solve a various conflicts with a many versions of uploading files, for example.  

Files with size more than 2000000 bytes split into parts and uploads.  

Documentation for work with RIAK-server:  
<https://github.com/lightuponcloud/dubstack/blob/master/API.md>

Source code:
<https://github.com/TrickyShit/LightClientUpload>  
