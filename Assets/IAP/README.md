# MangoMango IAP System

This folder contains scripts for managing In-App Purchases (IAP) in Unity using Unity's Purchasing system. The system is modular, event-driven, and supports both consumable and non-consumable products.

## Setup Instructions

1. **Place the IAP Manager Prefab**
   - Drag `Assets/IAP/Prefabs/IAP Manager.prefab` into your splash screen scene.
   - Assign the ScriptableObject (`IAP Data`) containing your product IDs and names to the prefab.

2. **Configure Products**
   - Open the `IAP Data` ScriptableObject.
   - Add products to the list and set their IDs according to the `ProductName` enum.
   - Ensure each product's store IDs (Google Play, App Store) match the enum entry.

3. **Use IAPButton for Purchase UI**
   - Add the `IAPButton` component to your purchase buttons.
   - Assign the corresponding `ProductName` and a price text component (e.g., TextMeshProUGUI).
   - The price text will be set automatically, and clicking the button will trigger the purchase.

4. **Use IAPEventTrigger for Event Handling**
   - Add the `IAPEventTrigger` component to a GameObject in your scene.
   - Configure it to listen for purchase success, failure, or already-owned events and trigger UnityEvents directly in the editor.

## Scripts Overview

### IAPManager.cs
- Central manager for all IAP operations, initialization, and purchase handling.

### IAPData.cs
- ScriptableObject for storing all available IAP items and their IDs.

### IAPButton.cs
- Attach to UI buttons to trigger purchases and display localized prices.

### IAPEventTrigger.cs
- Bind UnityEvents to purchase success, failure, or already-owned products.

## Notes
- Supports both Google Play and App Store product IDs.
- Handles duplicate product definitions.
- Designed for extensibility and easy Unity Editor integration.
