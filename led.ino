#include <WiFiS3.h>
#include <WiFiSSLClient.h>
#include <ArduinoMqttClient.h>
#include <ArduinoJson.h>

// ============================================================================
// CONFIGURATION
// ============================================================================

const char* ssid = "Bard";
const char* password = "PipiEntreAmis67";
const char* mqtt_server = "10.0.0.67";
const int mqtt_port = 8883;
const char* roomId = "201";
const char* lampId = "LED_1";

const char ca_cert[] = R"EOF(
-----BEGIN CERTIFICATE-----
MIIDtTCCAp2gAwIBAgIUETruQ+JBEU7Nj8r5CCPkbDlQ0/YwDQYJKoZIhvcNAQEL
BQAwajELMAkGA1UEBhMCRlIxETAPBgNVBAgMCEdyYW5kRXN0MRMwEQYDVQQHDApT
dHJhc2JvdXJnMRAwDgYDVQQKDAdGaWF0THV4MQwwCgYDVQQLDANJb1QxEzARBgNV
BAMMCkZpYXRMdXgtQ0EwHhcNMjYwMjA2MDkzODMxWhcNMzYwMjA0MDkzODMxWjBq
MQswCQYDVQQGEwJGUjERMA8GA1UECAwIR3JhbmRFc3QxEzARBgNVBAcMClN0cmFz
Ym91cmcxEDAOBgNVBAoMB0ZpYXRMdXgxDDAKBgNVBAsMA0lvVDETMBEGA1UEAwwK
RmlhdEx1eC1DQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAJD0wcgE
ykRV7bawZD42L/YiWMCW7j/6w9H4gy4P2P1MBfQWCXAYo1cvrgYfIkCL2B05OqdI
J6LYHRXiRa9RHDpXA2XfAsJMaA3wn77Zi/8kHyF/70rrLuy+T5bfvp8aAAv9Fiv/
x8bqIlhHQ/cRYJbZ49o8aYTdaeUyBcoTW34wxoGwY+SjEpTUUt5QVbt2nn1V8jT1
3Vuy4tu9JewKwBhZVBBTcDCDKQpa+ZRS21F8UOQvzlxXWG+X4guKQXoUYXK+C8JJ
4PD21yHmWb8cJCPZMhQAxokiK5/G82cQFPLSnOP3/fjjLUl/FdV+YD3oIBMbno0c
J1FLqxAfpps9VBUCAwEAAaNTMFEwHQYDVR0OBBYEFGOdWA/Izka2Tc+zkhpOjWca
EEjcMB8GA1UdIwQYMBaAFGOdWA/Izka2Tc+zkhpOjWcaEEjcMA8GA1UdEwEB/wQF
MAMBAf8wDQYJKoZIhvcNAQELBQADggEBAClcV0zm05YBmcQ6eWtrpvBSu/Zhr5Hl
ziyU/n25uAkdxJ4suudQ10iaYKGYhUWA4Cu51fi046oIxEYH4od5j9pDBhx7L17i
8164me7Xy67BaVbGnZlM9F8LlknB9mwrCjp8shJzh7RfDUOxNUiMwbx+++lnHECn
1JV3DXHKIIBokSP93bJNEdU7G6K0zqc6g/qfckAERMIBkPU7SYyYnX04Xg1bo9qV
bVs6Tz3Vq7Pv26FaItZMx0Q6+t+ZSUp0aX9NRh/ybNwOTvacSFUYAqbc2lG0bw7U
lsYNdZGljAo4//n5ag2GKU18b4XERJP5/izSjGgc6lIPTrUdra7ipYE=
-----END CERTIFICATE-----
)EOF";

// ============================================================================
// VARIABLES
// ============================================================================

WiFiSSLClient wifiClient;
MqttClient mqttClient(wifiClient);

const int ledPin = 10;

float targetLux = 500.0;
float currentLux = 0.0;
int ledPower = 0;
String currentMode = "Confort";

unsigned long bootTime = 0;
unsigned long lastHeartbeat = 0;
const unsigned long HEARTBEAT_INTERVAL = 120000;  // 2 minutes

// Fade lineaire
int targetPWM = 0;
int currentPWM = 0;
unsigned long fadeStartTime = 0;
const unsigned long FADE_DURATION = 5000;
bool isFading = false;

// ============================================================================
// FONCTIONS
// ============================================================================

void adjustLED() {
  float error = targetLux - currentLux;
  
  Serial.print("Target:");
  Serial.print(targetLux, 0);
  Serial.print(" Current:");
  Serial.print(currentLux, 0);
  Serial.print(" Error:");
  Serial.print(error, 0);
  Serial.print(" | ");
  
  if (abs(error) < 10) {
    Serial.println("OK");
    return;
  }
  
  int newTargetPWM;
  
  if (error > 0) {
    float ratio = error / targetLux;
    newTargetPWM = constrain(currentPWM + (255 * ratio), 0, 255);
    Serial.print("UP");
  } else {
    float ratio = abs(error) / targetLux;
    newTargetPWM = constrain(currentPWM - (currentPWM * ratio * 0.8), 0, 255);
    Serial.print("DOWN");
  }
  
  int maxChange = currentPWM / 2;
  if (maxChange < 20) maxChange = 20;
  
  int change = newTargetPWM - currentPWM;
  
  if (abs(change) > maxChange) {
    if (change > 0) {
      newTargetPWM = currentPWM + maxChange;
    } else {
      newTargetPWM = currentPWM - maxChange;
    }
    Serial.print(" [limite a 50%]");
  }
  
  if (abs(newTargetPWM - currentPWM) > 5) {
    targetPWM = newTargetPWM;
    fadeStartTime = millis();
    isFading = true;
    
    Serial.print(" -> Fade ");
    Serial.print(currentPWM);
    Serial.print("->");
    Serial.print(targetPWM);
    Serial.print(" (5s)");
  }
  
  Serial.println();
}

void updateFade() {
  if (!isFading) return;
  
  unsigned long elapsed = millis() - fadeStartTime;
  
  if (elapsed >= FADE_DURATION) {
    currentPWM = targetPWM;
    ledPower = currentPWM;
    isFading = false;
  } else {
    float progress = (float)elapsed / FADE_DURATION;
    int startPWM = ledPower;
    currentPWM = startPWM + (targetPWM - startPWM) * progress;
    ledPower = currentPWM;
  }
  
  analogWrite(ledPin, ledPower);
}

void publishLampState(bool isHeartbeat) {
  StaticJsonDocument<256> doc;
  doc["lampId"] = lampId;
  doc["room"] = roomId;
  doc["power"] = map(ledPower, 0, 255, 0, 100);
  doc["pwm"] = ledPower;
  doc["mode"] = currentMode;
  doc["target"] = (int)targetLux;
  doc["timestamp"] = millis();
  doc["uptime"] = millis() - bootTime;
  doc["heartbeat"] = isHeartbeat;  // NOUVEAU
  
  String output;
  serializeJson(doc, output);
  
  String topic = String("campus/lampe/") + roomId;
  
  mqttClient.beginMessage(topic);
  mqttClient.print(output);
  mqttClient.endMessage();
  
  if (isHeartbeat) {
    Serial.print(">>> HEARTBEAT: ");
  } else {
    Serial.print(">>> DATA: ");
  }
  Serial.println(output);
}

void onMqttMessage(int messageSize) {
  String topic = mqttClient.messageTopic();
  String msg = mqttClient.readString();
  
  StaticJsonDocument<200> doc;
  if (deserializeJson(doc, msg)) return;
  
  // Capteur
  if (topic.indexOf("capteur") >= 0) {
    if (doc.containsKey("lux")) {
      currentLux = doc["lux"];
      adjustLED();
      publishLampState(false);
    }
  }
  
  // Config
  else if (topic.indexOf("config") >= 0) {
    bool changed = false;
    
    if (doc.containsKey("target")) {
      targetLux = doc["target"];
      changed = true;
      Serial.print("New target: ");
      Serial.println(targetLux, 0);
    }
    
    if (doc.containsKey("mode")) {
      currentMode = doc["mode"].as<String>();
      Serial.print("New mode: ");
      Serial.println(currentMode);
    }
    
    if (changed && currentLux > 0) {
      adjustLED();
    }
    
    publishLampState(false);
  }
}

void reconnectMQTT() {
  if (mqttClient.connected()) return;
  
  Serial.print("MQTT...");
  
  String clientId = String("lampe_") + lampId;
  mqttClient.setId(clientId.c_str());
  
  if (mqttClient.connect(mqtt_server, mqtt_port)) {
    Serial.println(" OK");
    
    String capteurTopic = String("campus/capteur/") + roomId;
    String configTopic = String("campus/config/") + roomId;
    
    mqttClient.subscribe(capteurTopic);
    mqttClient.subscribe(configTopic);
    
    Serial.print("Sub: ");
    Serial.print(capteurTopic);
    Serial.print(", ");
    Serial.println(configTopic);
  } else {
    Serial.print(" ERR:");
    Serial.println(mqttClient.connectError());
  }
}

// ============================================================================
// SETUP & LOOP
// ============================================================================

void setup() {
  pinMode(ledPin, OUTPUT);
  Serial.begin(115200);
  delay(1000);
  
  bootTime = millis();
  
  Serial.println("\n=== LAMPE v2 ===");
  Serial.print("Salle: ");
  Serial.print(roomId);
  Serial.print(" | ID: ");
  Serial.println(lampId);
  Serial.println("Fade: 5s | Heartbeat: 2 min");
  
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println(" WiFi OK");
  Serial.println(WiFi.localIP());
  
  wifiClient.setCACert(ca_cert);
  mqttClient.onMessage(onMqttMessage);
  mqttClient.setKeepAliveInterval(60000);
  mqttClient.setConnectionTimeout(30000);
  
  reconnectMQTT();
  
  Serial.print("Target: ");
  Serial.print(targetLux, 0);
  Serial.println(" lux");
  Serial.println("=== PRET ===\n");
  
  publishLampState(false);
}

void loop() {
  mqttClient.poll();
  
  // Reconnexion MQTT
  static unsigned long lastReconnect = 0;
  if (!mqttClient.connected() && millis() - lastReconnect > 5000) {
    lastReconnect = millis();
    reconnectMQTT();
  }
  
  // Heartbeat toutes les 2 minutes
  if (millis() - lastHeartbeat >= HEARTBEAT_INTERVAL) {
    lastHeartbeat = millis();
    publishLampState(true);  // heartbeat=true
  }
  
  // Fade continu
  updateFade();
}