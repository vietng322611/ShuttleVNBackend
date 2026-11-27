resource "oci_core_network_security_group" "public" {
  compartment_id = var.compartment_ocid
  vcn_id         = var.vcn_id
  display_name   = "${var.project_name}-public-nsg"
}

resource "oci_core_network_security_group" "private" {
  compartment_id = var.compartment_ocid
  vcn_id         = var.vcn_id
  display_name   = "${var.project_name}-private-nsg"
}

resource "oci_core_network_security_group_security_rule" "public_ingress" {
  for_each = {
    ssh = {
      port   = 22
      source = var.ssh_allowed_cidr
    }
    http = {
      port   = 80
      source = "0.0.0.0/0"
    }
    https = {
      port   = 443
      source = "0.0.0.0/0"
    }
  }

  network_security_group_id = oci_core_network_security_group.public.id
  direction                 = "INGRESS"
  protocol                  = "6" // tcp
  source_type               = "CIDR_BLOCK"
  source                    = each.value.source
  stateless                 = false
  description               = "Allow ${each.key}"

  tcp_options {
    destination_port_range {
      min = each.value.port
      max = each.value.port
    }
  }
}

resource "oci_core_network_security_group_security_rule" "private_ingress" {
  for_each = {
    ssh     = 22
    backend = 8080
  }

  network_security_group_id = oci_core_network_security_group.private.id
  direction                 = "INGRESS"
  protocol                  = "6" // tcp
  source_type               = "NETWORK_SECURITY_GROUP"
  source                    = oci_core_network_security_group.public.id
  stateless                 = false
  description               = "Allow ${each.key} from public NSG"

  tcp_options {
    destination_port_range {
      min = each.value
      max = each.value
    }
  }
}

resource "oci_core_network_security_group_security_rule" "public_metrics" {
  for_each = {
    node_exporter = 9100
    cadvisor      = 8081
  }

  network_security_group_id = oci_core_network_security_group.public.id
  direction                 = "INGRESS"
  protocol                  = "6" // tcp
  source_type               = "NETWORK_SECURITY_GROUP"
  source                    = oci_core_network_security_group.private.id
  stateless                 = false
  description               = "Allow ${each.key} from private NSG"

  tcp_options {
    destination_port_range {
      min = each.value
      max = each.value
    }
  }
}

resource "oci_core_network_security_group_security_rule" "egress" {
  for_each = {
    public  = oci_core_network_security_group.public.id
    private = oci_core_network_security_group.private.id
  }

  network_security_group_id = each.value
  direction                 = "EGRESS"
  protocol                  = "all"
  destination_type          = "CIDR_BLOCK"
  destination               = "0.0.0.0/0"
  stateless                 = false
  description               = "Allow outbound traffic"
}
