variable "project_name" {
  type = string
}

variable "vpc_id" {
  type = string
}

variable "ssh_allowed_cidr" {
  type = string

  validation {
    condition = (
      can(cidrnetmask(var.ssh_allowed_cidr)) &&
      endswith(var.ssh_allowed_cidr, "/32")
    )
    error_message = "Use your public IPv4 address with /32."
  }
}
