#!/usr/bin/perl
use strict;

rename('server.py', 'server.py.orig') or die 'no input present';
my $input_file;
my $output_file;
open($input_file, '<', 'server.py.orig');
open($output_file, '>', 'server.py');
while(<$input_file>) {
  s/API_KEY =.*/API_KEY = 'abc123'/;
  s/UPLOADER_SCRIPT =.*/UPLOADER_SCRIPT = '.\/upload.sh'/;
  s/SSL_CERT_PATH =.*/SSL_CERT_PATH = '.\/cert.pem'/;
  s/SSL_KEY_PATH =.*/SSL_KEY_PATH = '.\/key.pem'/;
  print { $output_file } $_;
}
