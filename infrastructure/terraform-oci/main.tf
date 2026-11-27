data "oci_identity_availability_domains" "available" {
  compartment_id = var.tenancy_ocid
}

module "network" {
  source = "./modules/network"

  compartment_ocid = var.compartment_ocid
  project_name     = var.project_name

  vcn_cidr            = "10.0.0.0/16"
  public_subnet_cidr  = "10.0.1.0/24"
  private_subnet_cidr = "10.0.2.0/24"
}

module "security" {
  source = "./modules/security"

  compartment_ocid = var.compartment_ocid
  project_name     = var.project_name
  vcn_id           = module.network.vcn_id
  ssh_allowed_cidr = var.ssh_allowed_cidr
}

locals {
  selected_ad = (
    var.availability_domain != null
    ? var.availability_domain
    : data.oci_identity_availability_domains.available.availability_domains[0].name
  )

  ssh_public_key = trimspace(
    file(pathexpand(var.ssh_public_key_path))
  )
}

module "public_compute" {
  source = "./modules/compute"

  compartment_ocid    = var.compartment_ocid
  availability_domain = local.selected_ad
  display_name        = "${var.project_name}-public"

  subnet_id        = module.network.public_subnet_id
  nsg_ids          = [module.security.public_nsg_id]
  assign_public_ip = true

  image_ocid     = var.image_ocid
  ocpus          = 1
  memory_in_gbs  = 4
  boot_volume_gb = 50
  ssh_public_key = local.ssh_public_key
}

module "private_compute" {
  source = "./modules/compute"

  compartment_ocid    = var.compartment_ocid
  availability_domain = local.selected_ad
  display_name        = "${var.project_name}-private"

  subnet_id        = module.network.private_subnet_id
  nsg_ids          = [module.security.private_nsg_id]
  assign_public_ip = false

  image_ocid     = var.image_ocid
  ocpus          = 1
  memory_in_gbs  = 8
  boot_volume_gb = 50
  ssh_public_key = local.ssh_public_key
}
