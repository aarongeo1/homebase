import type { components } from './generated/schema';

// Friendly aliases for the generated contract types. Never hand-write an API
// shape: add it to contracts/openapi.yaml and run `npm run gen:api`.
type Schemas = components['schemas'];

export type ApiErrorBody = Schemas['ApiError'];
export type Health = Schemas['Health'];
export type Sun = Schemas['Sun'];
export type Presence = Schemas['Presence'];
export type Room = Schemas['Room'];
export type DeviceType = Schemas['DeviceType'];
export type DeviceDriver = Schemas['DeviceDriver'];
export type DeviceState = Schemas['DeviceState'];
export type Device = Schemas['Device'];
export type DeviceCommand = Schemas['DeviceCommand'];
export type DeviceSettings = Schemas['DeviceSettings'];
export type DeviceReport = Schemas['DeviceReport'];
export type Discovery = Schemas['Discovery'];
export type PairingRequest = Schemas['PairingRequest'];
export type Pairing = Schemas['Pairing'];
export type PairingCode = Schemas['PairingCode'];
export type Action = Schemas['Action'];
export type Scene = Schemas['Scene'];
export type SceneActivation = Schemas['SceneActivation'];
export type Weekday = Schemas['Weekday'];
export type TriggerField = Schemas['TriggerField'];
export type Trigger = Schemas['Trigger'];
export type Condition = Schemas['Condition'];
export type RunOutcome = Schemas['RunOutcome'];
export type Automation = Schemas['Automation'];
export type TraceStep = Schemas['TraceStep'];
export type Simulation = Schemas['Simulation'];
export type AutomationRun = Schemas['AutomationRun'];
