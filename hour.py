#!/usr/bin/env python3
"""
Planificateur Jour/Nuit pour FiatLux
Publie sur MQTT les transitions jour/nuit pour mettre les capteurs en veille
"""

import paho.mqtt.client as mqtt
import schedule
import time
import json
from datetime import datetime

# ============================================================================
# CONFIGURATION
# ============================================================================

MQTT_BROKER = "10.0.0.67"
MQTT_PORT = 8883
MQTT_TOPIC = "campus/config/schedule"

# Plages horaires
HEURE_JOUR = "07:00"    # Début journée
HEURE_NUIT = "20:00"    # Début nuit

# Certificat TLS (même CA que les Arduinos)
CA_CERT = r"C:\Users\raphu\mqtt-certs\ca.crt"

# ============================================================================
# MQTT CLIENT
# ============================================================================

client = mqtt.Client(client_id="scheduler_pc")
client.tls_set(ca_certs=CA_CERT)

def on_connect(client, userdata, flags, rc):
    if rc == 0:
        print("✓ Connecté au broker MQTT")
    else:
        print(f"✗ Erreur connexion MQTT: {rc}")

client.on_connect = on_connect

# ============================================================================
# FONCTIONS PLANIFICATION
# ============================================================================

def publish_mode(mode):
    """Publie le mode jour/nuit sur MQTT"""
    payload = {
        "mode": mode,
        "timestamp": int(time.time()),
        "datetime": datetime.now().strftime("%Y-%m-%d %H:%M:%S")
    }
    
    json_payload = json.dumps(payload)
    
    result = client.publish(MQTT_TOPIC, json_payload, qos=1, retain=True)
    
    if result.rc == mqtt.MQTT_ERR_SUCCESS:
        print(f">>> {mode.upper()} publié [{datetime.now().strftime('%H:%M:%S')}]")
        print(f"    {json_payload}")
    else:
        print(f"✗ Erreur publication: {result.rc}")

def activate_jour():
    """Active le mode JOUR (capteurs actifs)"""
    print("\n🌞 TRANSITION → JOUR")
    publish_mode("jour")

def activate_nuit():
    """Active le mode NUIT (capteurs en veille)"""
    print("\n🌙 TRANSITION → NUIT")
    publish_mode("nuit")

# ============================================================================
# MAIN
# ============================================================================

def main():
    print("=" * 60)
    print("PLANIFICATEUR JOUR/NUIT - FiatLux")
    print("=" * 60)
    print(f"Jour   : {HEURE_JOUR} - {HEURE_NUIT}")
    print(f"Nuit   : {HEURE_NUIT} - {HEURE_JOUR}")
    print(f"Broker : {MQTT_BROKER}:{MQTT_PORT}")
    print(f"Topic  : {MQTT_TOPIC}")
    print("=" * 60)
    
    # Connexion MQTT
    try:
        client.connect(MQTT_BROKER, MQTT_PORT, 60)
        client.loop_start()
    except Exception as e:
        print(f"✗ Erreur connexion: {e}")
        return
    
    # Attendre connexion
    time.sleep(2)
    
    # Détermine l'état initial
    now = datetime.now().time()
    heure_jour = datetime.strptime(HEURE_JOUR, "%H:%M").time()
    heure_nuit = datetime.strptime(HEURE_NUIT, "%H:%M").time()
    
    if heure_jour <= now < heure_nuit:
        print("\n✓ État actuel : JOUR")
        publish_mode("jour")
    else:
        print("\n✓ État actuel : NUIT")
        publish_mode("nuit")
    
    # Planification des transitions
    schedule.every().day.at(HEURE_JOUR).do(activate_jour)
    schedule.every().day.at(HEURE_NUIT).do(activate_nuit)
    
    print(f"\n✓ Planification active")
    print(f"  → Jour  à {HEURE_JOUR}")
    print(f"  → Nuit  à {HEURE_NUIT}")
    print("\n[Ctrl+C pour arrêter]\n")
    
    # Boucle principale
    try:
        while True:
            schedule.run_pending()
            time.sleep(60)  # Vérifie toutes les minutes
    except KeyboardInterrupt:
        print("\n\n✓ Arrêt du planificateur")
        client.loop_stop()
        client.disconnect()

if __name__ == "__main__":
    main()