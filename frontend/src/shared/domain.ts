export type Ticket = {
  id: string;
  organizationId: string;
  ticketNumber: string;
  category: string;
  title: string;
  description: string;
  priority: string;
  status: string;
  updatedAt: string;
  createdAt: string;
  assignedAgentId?: string;
  assignedExpertId?: string;
  finalRecommendation?: string;
  referralRecommendation?: string;
};

export type TicketMessage = {
  id: string;
  ticketId: string;
  senderUserId: string;
  messageType: string;
  body: string;
  createdAt: string;
};

export type TicketAttachment = {
  id: string;
  ticketId: string;
  messageId?: string;
  fileId: string;
  originalFilename: string;
  contentType: string;
  sizeBytes: number;
  checksum: string;
  uploadedBy?: string;
  createdAt: string;
};

export type Meeting = {
  id: string;
  subject: string;
  description: string;
  meetingType: string;
  startsAt?: string;
  endsAt?: string;
  status: string;
  location?: string;
  onlineLink?: string;
  notes?: string;
  requestedByUserId?: string;
  createdByUserId?: string;
};

export type Organization = {
  id: string;
  name: string;
  type: string;
  sector?: string;
  region?: string;
  status: string;
};

export type Subscription = {
  id: string;
  status: string;
  startsAt?: string;
  expiresAt?: string;
  offlinePaymentReference?: string;
};

export type SubscriptionInvitation = {
  id: string;
  organizationId: string;
  status: string;
  expiresAt: string;
};
export type Profile = {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  preferredLanguage: string;
  organizationId?: string;
};
