#include <WiFiS3.h>
#include <WiFiSSLClient.h>
#include <ArduinoMqttClient.h>
#include <ArduinoJson.h>
#include <WiFiUdp.h>
#include <NTPClient.h>

// ============================================================================
// CONFIGURATION
// ============================================================================

const char* ssid = "Bard";
const char* password = "PipiEntreAmis67";
const char* mqtt_server = "10.0.0.67";
const int mqtt_port = 8883;
const char* sensorId = "PHOTO_201";
const char* roomId = "201";

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

// NTP
WiFiUDP ntpUDP;
// ✅ Serveur NTP Orange
NTPClient timeClient(ntpUDP, "ntp.obspm.fr", 3600, 60000);

const int photoPin = A0;
String currentMode = "jour";
unsigned long bootTime = 0;

// Publication intelligente basée sur le delta
const unsigned long READ_INTERVAL = 10000;      // Lecture toutes les 10s
const unsigned long HEARTBEAT_INTERVAL = 120000; // Heartbeat 2 min
const float LUX_DELTA_THRESHOLD = 20.0;          // Seuil de changement : 20 lux
const int CONSECUTIVE_CHANGES = 2;               // 2 mesures consécutives avec delta

float lastPublishedLux = 0;
float lastReadLux = 0;
int consecutiveChanges = 0;

unsigned long lastRead = 0;
unsigned long lastHeartbeat = 0;

// ============================================================================
// FONCTIONS
// ============================================================================

float readLux() {
  int raw = analogRead(photoPin);
  return map(raw, 0, 1023, 0, 1000);
}

void publishData(bool isHeartbeat) {
  float luxToPublish = isHeartbeat ? lastReadLux : lastReadLux;
  
  StaticJsonDocument<256> doc;
  doc["sensorId"] = sensorId;
  doc["room"] = roomId;
  doc["lux"] = luxToPublish;
  doc["timestamp"] = timeClient.getEpochTime() * 1000UL;
  doc["mode"] = currentMode;
  doc["uptime"] = millis() - bootTime;
  doc["heartbeat"] = isHeartbeat;
  
  String output;
  serializeJson(doc, output);
  
  String topic = String("campus/capteur/") + roomId;
  mqttClient.beginMessage(topic);
  mqttClient.print(output);
  mqttClient.endMessage();
  
  if (isHeartbeat) {
    Serial.print(">>> HEARTBEAT: ");
  } else {
    Serial.print(">>> DELTA: ");
  }
  Serial.print(output);
  Serial.print(" (change: ");
  Serial.print(abs(luxToPublish - lastPublishedLux), 1);
  Serial.println(" lux)");
  
  // Met à jour la dernière valeur publiée
  if (!isHeartbeat) {
    lastPublishedLux = luxToPublish;
    consecutiveChanges = 0;  // Reset le compteur
  }
}

void onMqttMessage(int messageSize) {
  String topic = mqttClient.messageTopic();
  String msg = mqttClient.readString();
  
  StaticJsonDocument<200> doc;
  if (deserializeJson(doc, msg)) return;
  
  if (topic.indexOf("config") >= 0 && doc.containsKey("mode")) {
    String newMode = doc["mode"].as<String>();
    if (newMode != currentMode) {
      currentMode = newMode;
      Serial.print("Mode change: ");
      Serial.println(currentMode);
      
      // Reset les compteurs lors du changement de mode
      if (currentMode == "jour") {
        lastPublishedLux = lastReadLux;
        consecutiveChanges = 0;
      }
    }
  }
}

void reconnectMQTT() {
  if (mqttClient.connected()) return;
  
  Serial.print("MQTT...");
  
  String clientId = String("capteur_") + sensorId;
  mqttClient.setId(clientId.c_str());
  
  if (mqttClient.connect(mqtt_server, mqtt_port)) {
    Serial.println(" OK");
    
    String configTopic = String("campus/config/") + roomId;
    mqttClient.subscribe(configTopic);
    
    Serial.print("Sub: ");
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
  pinMode(photoPin, INPUT);
  Serial.begin(115200);
  delay(1000);
  
  bootTime = millis();
  
  Serial.println("\n=== CAPTEUR PHOTORESISTANCE v4 - DELTA ===");
  Serial.print("Salle: ");
  Serial.print(roomId);
  Serial.print(" | ID: ");
  Serial.println(sensorId);
  Serial.print("Seuil delta: ");
  Serial.print(LUX_DELTA_THRESHOLD, 0);
  Serial.print(" lux | Consecutives: ");
  Serial.println(CONSECUTIVE_CHANGES);
  
  // WiFi
  WiFi.begin(ssid, password);
  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }
  Serial.println(" WiFi OK");
  Serial.println(WiFi.localIP());
  
  // NTP SYNC
  Serial.print("NTP...");
  timeClient.begin();
  int attempts = 0;
  while (!timeClient.update() && attempts < 10) {
    timeClient.forceUpdate();
    delay(1000);
    Serial.print(".");
    attempts++;
  }
  if (attempts < 10) {
    Serial.println(" OK");
    Serial.print("Heure: ");
    Serial.println(timeClient.getFormattedTime());
  } else {
    Serial.println(" TIMEOUT");
  }
  
  // MQTT
  wifiClient.setCACert(ca_cert);
  mqttClient.onMessage(onMqttMessage);
  mqttClient.setKeepAliveInterval(60000);
  mqttClient.setConnectionTimeout(30000);
  
  reconnectMQTT();
  
  Serial.println("=== PRET ===\n");
  
  // Lecture initiale
  lastReadLux = readLux();
  lastPublishedLux = lastReadLux;
  publishData(false);
}

void loop() {
  mqttClient.poll();
  timeClient.update();
  
  // Reconnexion MQTT
  static unsigned long lastReconnect = 0;
  if (!mqttClient.connected() && millis() - lastReconnect > 5000) {
    lastReconnect = millis();
    reconnectMQTT();
  }
  
  // Lecture toutes les 10s (mode jour uniquement)
  if (currentMode == "jour" && millis() - lastRead >= READ_INTERVAL) {
    lastRead = millis();
    
    float currentLux = readLux();
    float delta = abs(currentLux - lastPublishedLux);
    
    Serial.print("Mesure: ");
    Serial.print(currentLux, 1);
    Serial.print(" lux | Delta: ");
    Serial.print(delta, 1);
    Serial.print(" lux | ");
    
    // Vérifie si le delta dépasse le seuil
    if (delta >= LUX_DELTA_THRESHOLD) {
      consecutiveChanges++;
      Serial.print("Change detected (");
      Serial.print(consecutiveChanges);
      Serial.print("/");
      Serial.print(CONSECUTIVE_CHANGES);
      Serial.println(")");
      
      // Publie après 2 mesures consécutives avec delta
      if (consecutiveChanges >= CONSECUTIVE_CHANGES) {
        lastReadLux = currentLux;
        publishData(false);
      }
    } else {
      // Pas de changement significatif
      if (consecutiveChanges > 0) {
        Serial.print("Reset counter (delta too small)");
      } else {
        Serial.print("Stable");
      }
      Serial.println();
      consecutiveChanges = 0;
    }
    
    lastReadLux = currentLux;
  }
  
  // Heartbeat toutes les 2 min
  if (millis() - lastHeartbeat >= HEARTBEAT_INTERVAL) {
    lastHeartbeat = millis();
    publishData(true);
  }
}