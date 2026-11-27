variable "compartment_ocid" {
  type = string
}

variable "availability_domain" {
  type = string
}

variable "display_name" {
  type = string
}

variable "subnet_id" {
  type = string
}

variable "nsg_ids" {
  type = list(string)
}

variable "assign_public_ip" {
  type = bool
}

variable "image_ocid" {
  type = string
}

variable "shape" {
  type    = string
  default = "VM.Standard.A1.Flex"
}

variable "ocpus" {
  type = number
}

variable "memory_in_gbs" {
  type = number
}

variable "boot_volume_gb" {
  type    = number
  default = 50
}

variable "ssh_public_key" {
  type = string
}
