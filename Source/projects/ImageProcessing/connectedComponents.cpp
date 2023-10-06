/*  ------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
 *  ------------------------------------------------------------------------------------------
 */

#include "stdafx.h"
#include "connectedComponents.h"

namespace createdataset
{
  void test_Set()
  {
    Set<short> s1, s2, s3, s4;

    // Sets are initially unique
    if (s1.find() == s2.find())
      throw std::exception("Internal error in Set.");

    // The union of two sets contains all members of both sets
    Set<short>::unite(&s1, &s2);
    if (s1.find() != s2.find())
      throw std::exception("Internal error in Set.");

    // Union is transitive

    Set<short>::unite(&s2, &s3);
    if (s2.find() != s1.find() || s3.find() != s2.find())
      throw std::exception("Internal error in Set.");

    Set<short>::unite(&s4, &s3);
    if (s4.find() != s3.find() || s3.find() != s1.find() || s3.find() != s2.find())
      throw std::exception("Internal error in Set.");
  }


  template<>
  void