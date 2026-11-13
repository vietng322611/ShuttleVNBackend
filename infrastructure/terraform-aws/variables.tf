variable "project_name" {
  type        = string
  description = "Name of the project"
}

variable "ami_id" {
  type        = string
  description = "Pinned Ubuntu amd64 AMI ID in Singapore."
}

variable "ssh_public_key" {
  type        = string
  description = "Contents of the SSH public key, not a path. CI has no operator filesystem to read a path from."
  sensitive   = true
}

variable "enable_nat_gateway" {
  type        = bool
  description = "NAT gateway costs ~$43/month while it exists. Off means the private host loses outbound access."
  default     = true
}

variable "ssh_allowed_cidr" {
  type        = string
  description = "Administrator public IPv4 address with /32."
}