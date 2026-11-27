variable "tenancy_ocid" {
  type        = string
  description = "OCID of your OCI tenancy (also the root compartment)."
}

variable "compartment_ocid" {
  type        = string
  description = "OCID of a compartment where infrastructure will be created. Root tenancy OCID works if permitted."
}

variable "ssh_public_key_path" {
  type        = string
  description = "Absolute filesystem path to SSH .pub file inside WSL, e.g. /home/alice/.ssh/id_ed25519.pub"
}

variable "ssh_allowed_cidr" {
  type        = string
  description = "Your public IPv4 address with /32 suffix, e.g. 203.0.113.10/32."
  validation {
    condition = (
      can(cidrnetmask(var.ssh_allowed_cidr)) &&
      endswith(var.ssh_allowed_cidr, "/32")
    )
    error_message = "Use your public IPv4 as a /32 CIDR."
  }
}

variable "availability_domain" {
  type        = string
  description = "Optional AD name. Defaults to first AD returned for tenancy."
  default     = null
}

variable "region" {
  type        = string
  description = "OCI region where infrastructure will be created."
  default     = "ap-singapore-1"
}

variable "project_name" {
  type        = string
  description = "Prefix for resource display names."
  default     = "shuttlevn"
}

variable "image_ocid" {
  type        = string
  description = "Pinned Ubuntu 24.04 ARM64 image OCID for the selected region."
}
