'use strict';

var crypto = require('crypto');
const fs = require('fs');
const { exec } = require('child_process');

// Parse args
var argv = require('yargs')
  .usage('Usage: $0 --file <filename>')
  .option('file', {
    alias: 'f',
    describe: 'simulator config file',
    type: 'string',
    demandOption: true
  })
  .argv;

var simulatorName = "";
var provisioningHost = "";
var idScope = "";
var symmetricKey = "";

var simulators = [];

try {
    const jsonFile = fs.readFileSync(argv.file, 'utf8');
    const configData = JSON.parse(jsonFile);

    simulatorName = configData.name;
    console.log(simulatorName + " started. ------------------");

    provisioningHost = configData.dps.provisionUrl;
    idScope = configData.dps.idScope;
    symmetricKey = configData.dps.groupSymmKey;

    simulators = configData.simulators;

} catch (error) {
    console.error("Can't find config file. (simulator-config.json)");
    return -1;
}

var computeDerivedSymmetricKey = (masterKey, regId) => 
    crypto.createHmac('SHA256', Buffer.from(masterKey, 'base64'))
            .update(regId, 'utf8')
            .digest('base64');

const _sleep = (delay) => new Promise((resolve) => setTimeout(resolve, delay));

const start = async () => {

    for (var j = 0 ; j < simulators.length ; j++) {
        var simulator = simulators[j];    
        var cnt = simulator.count;

        for(var i = 0 ; i < cnt ; i++) {
            var num = String(i+1).padStart(2,'0');
            var regid = `${simulator.prefix}${num}`;
            var key = computeDerivedSymmetricKey(symmetricKey, regid);
            var cmd = `node ${simulator.filename} --name "${simulator.name}" --regid "${regid}" --symmkey "${key}" --idscope "${idScope}" --provurl "${provisioningHost}" --intervalsec ${simulator.intervalSec}`;

            //console.log(`"${simulator.name}": ${regid}`);
            console.log(cmd);

            await _sleep(1500);

            // run a simulator
            const myShellScript = exec(cmd, (err, stdout, stderr) => {
                if (err) {
                //some err occurred
                console.error(err)
                } else {
                // the *entire* stdout and stderr (buffered)
                console.log(`stdout: ${stdout}`);
                console.log(`stderr: ${stderr}`);
                }
            });
            
            myShellScript.stdout.on('data', (data)=>{
                console.log(data); 
                // do whatever you want here with data
            });
            
            myShellScript.stderr.on('data', (data)=>{
                console.error(data);
            });
        }
    };
}

start();