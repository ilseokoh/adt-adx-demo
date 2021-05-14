'use strict';

var iotHubTransport = require('azure-iot-device-mqtt').Mqtt;
var Client = require('azure-iot-device').Client;
var Message = require('azure-iot-device').Message;
var crypto = require('crypto');
const uuidv4 = require('uuid/v4');

var ProvisioningTransport = require('azure-iot-provisioning-device-mqtt').Mqtt;

var SymmetricKeySecurityClient = require('azure-iot-security-symmetric-key').SymmetricKeySecurityClient;
var ProvisioningDeviceClient = require('azure-iot-provisioning-device').ProvisioningDeviceClient;

var argv = require('yargs')
  .usage('Usage: $0 --name <simulator name> --regid <dps registration key> --symmkey <dps group enrollment key> --idscope <dps id scope> --provurl <provisioning url> --intervalsec <interval in second>')
  .option('name', {
    alias: 'n',
    describe: 'simulator name',
    type: 'string',
    demandOption: true
  })
  .option('regid', {
    alias: 'r',
    describe: 'dps registration key',
    type: 'string',
    demandOption: true
  })
  .option('symmkey', {
    alias: 'k',
    describe: 'dps group enrollment key',
    type: 'string',
    demandOption: true
  })
  .option('idscope', {
    alias: 's',
    describe: 'dps id scope',
    type: 'string',
    demandOption: true
  })
  .option('provurl', {
    alias: 'u',
    describe: 'provisioning url',
    type: 'string',
    demandOption: true
  })
  .option('intervalsec', {
    alias: 'i',
    describe: 'interval in second',
    type: 'string',
    demandOption: true
  })
  .argv;

  var provisioningSecurityClient = new SymmetricKeySecurityClient(argv.regid, argv.symmkey);

  var provisioningClient = ProvisioningDeviceClient.create(argv.provurl, argv.idscope, new ProvisioningTransport(), provisioningSecurityClient);

  // Register the device.
provisioningClient.register(function (err, result) {
    if (err) {
        console.log("error registering device: " + err);
    } else {
        var connectionString = 'HostName=' + result.assignedHub + ';DeviceId=' + result.deviceId + ';SharedAccessKey=' + argv.symmkey;

        var hubClient = Client.fromConnectionString(connectionString, iotHubTransport);

        hubClient.open(function (err) {
            if (err) {
                console.error('Could not connect: ' + err.message);
            } else {
                console.log(argv.regid + ": Connected.");

                // Create a message and send it to the IoT Hub every two seconds
                var sendInterval = setInterval(function () {
                  var temperature = 20 + (Math.random() * 10); // range: [20, 30]
                  var data = JSON.stringify({ SensorValue: temperature, sentTime: new Date() });

                  var message = new Message(data);
                  message.properties.add('sensorType', 'temperature');

                  console.log(argv.regid + ' Sending message: ' + message.getData());
                  hubClient.sendEvent(message, printResultFor(argv.regid + ': send'));
                }, argv.intervalsec * 1000);

                hubClient.on('error', function (err) {
                  console.error(err.message);
                });

                hubClient.on('disconnect', function () {
                  clearInterval(sendInterval);
                  hubClient.removeAllListeners();
                });
            }
        });
    }
});

// Helper function to print results in the console
function printResultFor(op) {
  return function printResult(err, res) {
    if (err) console.log(op + ' error: ' + err.toString());
  };
}