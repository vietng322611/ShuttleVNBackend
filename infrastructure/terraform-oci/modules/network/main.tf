resource "oci_core_vcn" "this" {
  compartment_id = var.compartment_ocid
  display_name   = "${var.project_name}-vcn"
  cidr_blocks    = [var.vcn_cidr]
  dns_label      = "shuttlevn"
}

resource "oci_core_internet_gateway" "this" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.this.id
  display_name   = "${var.project_name}-igw"
  enabled        = true
}

resource "oci_core_nat_gateway" "this" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.this.id
  display_name   = "${var.project_name}-nat"
  block_traffic  = false
}

resource "oci_core_route_table" "public" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.this.id
  display_name   = "${var.project_name}-public-rt"

  route_rules {
    destination       = "0.0.0.0/0"
    destination_type  = "CIDR_BLOCK"
    network_entity_id = oci_core_internet_gateway.this.id
  }
}

resource "oci_core_route_table" "private" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.this.id
  display_name   = "${var.project_name}-private-rt"

  route_rules {
    destination       = "0.0.0.0/0"
    destination_type  = "CIDR_BLOCK"
    network_entity_id = oci_core_nat_gateway.this.id
  }
}

resource "oci_core_security_list" "baseline" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.this.id
  display_name   = "${var.project_name}-baseline-sl"
}

resource "oci_core_subnet" "public" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.this.id
  display_name   = "${var.project_name}-public-subnet"

  cidr_block     = var.public_subnet_cidr
  dns_label      = "public"
  route_table_id = oci_core_route_table.public.id

  prohibit_public_ip_on_vnic = false
  security_list_ids          = [oci_core_security_list.baseline.id]
}

resource "oci_core_subnet" "private" {
  compartment_id = var.compartment_ocid
  vcn_id         = oci_core_vcn.this.id
  display_name   = "${var.project_name}-private-subnet"

  cidr_block     = var.private_subnet_cidr
  dns_label      = "private"
  route_table_id = oci_core_route_table.private.id

  prohibit_public_ip_on_vnic = true
  security_list_ids          = [oci_core_security_list.baseline.id]
}
