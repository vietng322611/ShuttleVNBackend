variable "project_name" {
  type = string
}

variable "availability_zone" {
  type = string
}

variable "enable_nat_gateway" {
  type        = bool
  description = "Create NAT Gateway, its Elastic IP and private default route."
  default     = false
}
