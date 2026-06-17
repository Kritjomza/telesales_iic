export const CustomerStatuses = ["New", "Wait", "Sent", "Win", "Lost"] as const;
export type CustomerStatus = typeof CustomerStatuses[number];

export const DeviceStatuses = ["New", "Win", "Lost"] as const;
export type DeviceStatus = typeof DeviceStatuses[number];

export const ProjectStatuses = ["Discuss", "Quotation", "Win", "Lost", "Hold", "Cancel"] as const;
export type ProjectStatus = typeof ProjectStatuses[number];
