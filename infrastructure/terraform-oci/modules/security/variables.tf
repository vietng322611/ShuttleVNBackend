variable "compartment_ocid" {
  type        = string
  description = "Compartment where NSGs will be created."
}

variable "project_name" {
  type        = string
  description = "Prefix for resource display names."
}

variable "vcn_id" {
  type        = string
  description = "VCN containing both NSGs."
}

variable "ssh_allowed_cidr" {
  type        = string
  description = "Administrator public IPv4 address with /32 suffix."
}
