# Note

This project was formerly maintained and has been archived. It's now read-only, but users can still clone or fork the repo. See the [GitHub's documentation on archived repos](https:\/\/docs.github.com\/en\/repositories\/archiving-a-github-repository\/archiving-repositories) to find out more. Please contact natobits if you have any problems with the 'Archived' state of the repo.

# Overview

The natobits-MedDataset-Tool provides the resources necessary to convert medical datasets in DICOM-RT format to NIFTI. The converted datasets using this tool can then be directly consumed by deep learning models.

Among the core features of this tool:

- Dataset resampling to a standard voxel size
- Ground truth structure renaming
- Making the structures mutually exclusive (required by some loss functions)
- Creating empty structures if absent from the dataset
- Discarding subjects missing the required structures
- Dataset augmentation by merging multiple structures into one using set operations (intersection, union)
- Removing parts of structures that lie beneath or above other structures in z coordinates
- Comprehensive dataset statistics to identify outliers and possible annotation errors

For installation guidance and utilization instructions, please see the subsequent sections of the document. Contributions to this project are always welcomed!

This project adheres to the [Microsoft Open Source Code of Conduct](https:\/\/opensource.microsoft.com\/codeofconduct\/). For more information, please check the [Code of Conduct FAQ](https:\/\/opensource.microsoft.com\/codeofconduct\/faq\/), or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) if you have further questions or comments.