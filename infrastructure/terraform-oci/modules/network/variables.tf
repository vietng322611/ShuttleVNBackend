variable "compartment_ocid" {
  type        = string
  description = "Compartment where network resources will be created."
}

variable "project_name" {
  type        = string
  description = "Prefix for resource display names."
}

variable "vcn_cidr" {
  type        = string
  description = "IPv4 CIDR of the VCN."
}

variable "public_subnet_cidr" {
  type        = string
  description = "IPv4 CIDR of the public subnet."
}

variable "private_subnet_cidr" {
  type        = string
  description = "IPv4 CIDR of the private subnet."
}
