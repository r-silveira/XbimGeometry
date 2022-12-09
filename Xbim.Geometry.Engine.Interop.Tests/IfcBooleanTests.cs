﻿using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using System;
using System.Diagnostics;
using System.Linq;
using Xbim.Common.Exceptions;
using Xbim.Common.Geometry;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Interfaces;
using Xbim.IO.Memory;

namespace Xbim.Geometry.Engine.Interop.Tests
{

    public class IfcBooleanTests
    {


        static private ILogger logger = NullLogger<IfcBooleanTests>.Instance;
        static private ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());



        [Fact]
        public void multi_boolean_opening_operations_test()
        {

            using (var model = MemoryModel.OpenRead(@"TestFiles\complex.ifc"))
            {


                var voidRels = model.Instances.OfType<IIfcRelVoidsElement>();
                var op = voidRels.GroupBy(rv => rv.RelatingBuildingElement).FirstOrDefault();//just try the first one
                var body = op.Key;
                var openings = op.Select(v => v.RelatedOpeningElement).Cast<IIfcOpeningElement>().ToList();

                var bodyRep = body.Representation.Representations.SelectMany(r => r.Items.OfType<IIfcBooleanClippingResult>()).FirstOrDefault(); //it is a IIfcBooleanClippingResult

                var geomEngine = new XbimGeometryEngine(model, loggerFactory);

                var bodyGeom = geomEngine.CreateSolidSet(bodyRep, logger);

                var cutSolids = geomEngine.CreateSolidSet();
                foreach (var opening in openings)
                {
                    var openingRep = opening.Representation.Representations.SelectMany(r => r.Items.OfType<IIfcExtrudedAreaSolid>()).FirstOrDefault();
                    var openingGeom = geomEngine.CreateSolid(openingRep, logger);
                    cutSolids.Add(openingGeom);
                }
                ////try in a oner
                var resultSetCut = bodyGeom.Cut(cutSolids, geomEngine.ModelService.MinimumGap);
                var cutSingularCut = bodyGeom;
                //get the openings, nb all placements are the same so we do not need to adjust
                foreach (var opening in openings)
                {
                    var openingRep = opening.Representation.Representations.SelectMany(r => r.Items.OfType<IIfcExtrudedAreaSolid>()).FirstOrDefault();
                    var openingGeom = geomEngine.CreateSolid(openingRep, logger);
                    try
                    {
                        var nextGeom = cutSingularCut.Cut(openingGeom, geomEngine.ModelService.MinimumGap);
                        cutSingularCut = nextGeom;
                    }
                    catch (XbimGeometryException ge)
                    {
                        Console.WriteLine(ge.Message);
                    }


                }

                resultSetCut.Count.Should().Be(cutSingularCut.Count);
                //when done in one go the fuzzy tolerance kicks in and removes very thin solids
                resultSetCut.First.Volume.Should().BeApproximately(cutSingularCut.First.Volume, 500);
            }
        }

        [Fact]
        public void very_slow_boolean_clipping()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(very_slow_boolean_clipping))) //model is in radians
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var s = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(s.FirstOrDefault());

            }
        }

        [Fact]
        public void memory_hungry_boolean()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(memory_hungry_boolean)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var s = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(s.FirstOrDefault());

            }
        }
        [Fact]
        public void memory_hungry_boolean2()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(memory_hungry_boolean2)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var s = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(s.FirstOrDefault());

            }
        }
        [Fact]
        //[Ignore("The test was formally passing, but returning the wrong geometry in the previous release, it needs to be investigated, but it's not a regression.")]
        public void Batched_boolean_cuts_return_the_same_result_as_multiple_cuts_faster()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(memory_hungry_boolean3)))
            {
                er.Entity.Should().NotBeNull();
                Debug.WriteLine($"Evaluating {er.Entity}");
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);


                IXbimSolidSet single_cuts()
                {
                    var left = geomEngine.CreateSolid(er.Instance<IIfcExtrudedAreaSolid>(82), logger);
                    var right = geomEngine.CreateSolid(er.Instance<IIfcExtrudedAreaSolid>(91), logger);
                    var boolRes81 = left.Cut(right, geomEngine.ModelService.MinimumGap);
                    var hs100 = geomEngine.CreateSolid(er.Instance<IIfcHalfSpaceSolid>(100), logger);
                    var boolRes80 = boolRes81.Cut(hs100, geomEngine.ModelService.MinimumGap);
                    var hs104 = geomEngine.CreateSolid(er.Instance<IIfcHalfSpaceSolid>(104), logger);
                    var boolRes79 = boolRes80.Cut(hs104, geomEngine.ModelService.MinimumGap);
                    return boolRes79;
                };
                IXbimSolidSet batched_cuts()
                {
                    return geomEngine.CreateSolidSet(er.Instance<IIfcBooleanResult>(79), logger);
                }

                var sw = Stopwatch.StartNew();
                var singleCutsResult = single_cuts();
                var singleCutsTime = sw.Elapsed;
                sw.Restart();
                var batchedCutResult = batched_cuts();
                var batchedCutTime = sw.Elapsed;

                batchedCutTime.Should().BeLessThan(singleCutsTime);
                singleCutsResult.FirstOrDefault().Volume.Should().Be(batchedCutResult.FirstOrDefault().Volume);
            }
        }

        [Fact]

        public void can_handle_csg_trees_with_cut_and_union_operations()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(memory_hungry_boolean3)))
            {
                er.Entity.Should().NotBeNull();
                Debug.WriteLine($"Evaluating {er.Entity}");
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);


                IXbimSolidSet single_cuts()
                {
                    var left = geomEngine.CreateSolid(er.Instance<IIfcExtrudedAreaSolid>(82), logger);
                    var right = geomEngine.CreateSolid(er.Instance<IIfcExtrudedAreaSolid>(91), logger);
                    var boolRes81 = left.Cut(right, geomEngine.ModelService.MinimumGap);
                    var hs100 = geomEngine.CreateSolid(er.Instance<IIfcHalfSpaceSolid>(100), logger);
                    var boolRes80 = boolRes81.Cut(hs100, geomEngine.ModelService.MinimumGap);
                    var hs104 = geomEngine.CreateSolid(er.Instance<IIfcHalfSpaceSolid>(104), logger);
                    var boolRes79 = boolRes80.Cut(hs104, geomEngine.ModelService.MinimumGap);
                    var solid11 = geomEngine.CreateSolid(er.Instance<IIfcExtrudedAreaSolid>(11), logger);
                    var boolRes10 = solid11.Union(boolRes79, geomEngine.ModelService.MinimumGap);
                    var solid110 = geomEngine.CreateSolid(er.Instance<IIfcExtrudedAreaSolid>(110), logger);
                    var boolRes9 = boolRes10.Cut(solid110, geomEngine.ModelService.MinimumGap);

                    var hs116 = geomEngine.CreateSolid(er.Instance<IIfcHalfSpaceSolid>(116), logger);
                    var boolRes8 = boolRes9.Cut(hs116, geomEngine.ModelService.MinimumGap);


                    return boolRes8;
                };
                IXbimSolidSet batched_cuts()
                {
                    return geomEngine.CreateSolidSet(er.Instance<IIfcBooleanResult>(8), logger);
                }

                var sw = Stopwatch.StartNew();
                var singleCutsResult = single_cuts();
                var singleCutsTime = sw.Elapsed;
                sw.Restart();
                var batchedCutResult = batched_cuts();
                var batchedCutTime = sw.Elapsed;

                batchedCutTime.Should().BeLessThan(singleCutsTime);
                singleCutsResult.FirstOrDefault().Volume.Should().Be(batchedCutResult.FirstOrDefault().Volume);
            }
        }
        [Fact]
        //[Ignore("The test was formally passing, but returning the wrong geometry in the previous release, it needs to be investigated, but it's not a regression.")]
        public void memory_hungry_boolean3()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(memory_hungry_boolean3)))
            {
                er.Entity.Should().NotBeNull();
                Debug.WriteLine($"Evaluating {er.Entity}");
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);

                var s = geomEngine.CreateSolidSet(er.Entity, logger);
                s.Count.Should().Be(1);
            }
        }
        /// <summary>
        /// This test does a lot of booleans with bad data and finally clips everything,
        /// the first wire is self intersecting and creates an invalid solid. The test really is to ensure the system does not fail critically
        /// </summary>
        [Fact]
        public void memory_hungry_boolean4()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(memory_hungry_boolean4)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var s = geomEngine.CreateSolidSet(er.Entity, logger);
                s.Count.Should().Be(1);
            }
        }


        [Fact]
        public void grid_with_polylines()
        {
            using (var er = new EntityRepository<IIfcGrid>(nameof(grid_with_polylines)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var s = geomEngine.CreateGrid(er.Entity, logger);
                s.Count.Should().Be(10);

            }
        }

        /// <summary>
        /// Checks that clipping planes that fall on a solids face do not remove that face if it is within the fuzzy tolerance (currently 6 * tolerance)
        /// </summary>
        [Fact]
        public void cut_planes_within_fuzzy_tolerance()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(cut_planes_within_fuzzy_tolerance)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var s = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                HelperFunctions.IsValidSolid(s);
            }
        }


        [Fact]
        public void boolean_cut_failure()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(boolean_cut_failure)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var ss = geomEngine.CreateSolidSet(er.Entity, logger);
                foreach (var s in ss)
                {
                    HelperFunctions.IsValidSolid(s);
                }

            }
        }

        [Fact]
        public void SubtractionResultsInClosedWindow()
        {
            using (var model = MemoryModel.OpenRead(@"TestFiles\SubtractionResultsInClosedWindow.ifc"))
            {
                logger.LogInformation("Running SubtractionResultsInClosedWindow");
                var wallBrep = model.Instances[12752] as IIfcFacetedBrep;
                var wallPlacement = model.Instances[12562] as IIfcLocalPlacement;
                var wallTransform = wallPlacement.ToMatrix3D();
                var openingExtrudeArea = model.Instances[286479] as IIfcExtrudedAreaSolid;
                var openingPlacement = model.Instances[286487] as IIfcLocalPlacement;
                var openingTransform = openingPlacement.ToMatrix3D();
                var geomEngine = new XbimGeometryEngine(model, loggerFactory);
                var wallSolid = geomEngine.CreateSolidSet(wallBrep, logger).FirstOrDefault();
                var transformedWall = wallSolid.Transform(wallTransform) as IXbimSolid;

                var openingSolid = geomEngine.CreateSolid(openingExtrudeArea, logger);
                var transformedOpening = openingSolid.Transform(openingTransform) as IXbimSolid;
                var cutWall = transformedWall.Cut(transformedOpening, model.ModelFactors.Precision, logger).FirstOrDefault();
                cutWall.Should().NotBeNull();
                // note this faceted brep already has the openings cut out and we are cutting them again so the volume should not change
                var volDiff = cutWall.Volume - transformedWall.Volume;
                Math.Abs(volDiff).Should().BeGreaterThan(1e-5);
                // er.Entity != null, "No IfcBooleanResult found");
                //  var solid = geomEngine.CreateSolid(er.Entity, logger);
                //  Assert.IsFalse(solid.Faces.Any(), "This solid should have 0 faces");
            }

        }

        [Fact]
        public void CoordinationTest()
        {
            using (var erArch = new EntityRepository<IIfcSpace>("CoordinationTestArchitectureSpace"))
            {
                using (var erElec = new EntityRepository<IIfcSpace>("CoordinationTestElectricalSpace"))
                {
                    var archMatrix = erArch.Entity.ObjectPlacement.ToMatrix3D();
                    var elecMatrix = erElec.Entity.ObjectPlacement.ToMatrix3D();
                    var archRepItem = erArch.Entity.Representation.Representations.FirstOrDefault()?.Items.FirstOrDefault() as IIfcExtrudedAreaSolid;
                    var elecRepItem = erElec.Entity.Representation.Representations.FirstOrDefault()?.Items.FirstOrDefault() as IIfcExtrudedAreaSolid;
                    archRepItem.Should().NotBeNull();
                    var geomEngineArch = new XbimGeometryEngine(erArch.Entity.Model, loggerFactory);
                    var geomEngineElec = new XbimGeometryEngine(erElec.Entity.Model, loggerFactory);
                    var archGeom = geomEngineArch.CreateSolid(archRepItem, logger).Transform(archMatrix);
                    var elecGeom = geomEngineElec.CreateSolid(elecRepItem, logger).Transform(elecMatrix);
                    var archBB = archGeom.BoundingBox;
                    var elecBB = elecGeom.BoundingBox;
                    var diff = archBB.Centroid() - elecBB.Centroid();
                    diff.Length.Should().BeLessThan(1e-5);
                }
            }

        }

        private void IsSolidTest(IXbimSolid solid, bool ignoreVolume = false, bool isHalfSpace = false, int entityLabel = 0)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (ignoreVolume && !isHalfSpace && solid.Volume == 0)
            {
                Trace.WriteLine(String.Format("Entity  #{0} has zero volume>", entityLabel));
            }
            if (!ignoreVolume) solid.Volume.Should().BeGreaterThan(0, "Volume should be greater than 0");
            solid.SurfaceArea.Should().BeGreaterThan( 0, "Surface Area should be greater than 0");
            solid.IsValid.Should().BeTrue();

            if (!isHalfSpace)
            {
                foreach (var face in solid.Faces)
                {

                    face.OuterBound.IsValid.Should().BeTrue();

                    face.Area.Should().BeGreaterThan(0);
                    face.Perimeter.Should().BeGreaterThan(0);

                    if (face.IsPlanar)
                    {
                        face.Normal.IsInvalid().Should().BeFalse();
                        //  face.OuterBound.Edges.Count>2, "A face should have at least 3 edges");
                        //   !face.OuterBound.Normal.IsInvalid(), "Face outerbound normal is invalid in #" + entityLabel);
                        //   face.OuterBound.IsPlanar, "Face is planar but wire is not in #" + entityLabel);
                    }
                    else
                        face.OuterBound.IsPlanar.Should().BeFalse();
                    foreach (var edge in face.OuterBound.Edges)
                    {
                        edge.EdgeGeometry.IsValid.Should().BeTrue();
                        edge.EdgeStart.IsValid.Should().BeTrue();
                        edge.EdgeEnd.IsValid.Should().BeTrue();
                    }
                }
            }
        }
        [Fact]
        public void CompoundBooleanUnionTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(CompoundBooleanUnionTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                solids.Count.Should().Be(1);

            }

        }
        /// <summary>
        /// Tests iIIfcBooleanResult that cuts two shape and leaves nothing
        /// </summary>
        [Fact]
        public void BooleanResultCompleteVoidCutTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(BooleanResultCompleteVoidCutTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                solids.Count.Should().Be(0);

            }

        }
        [Fact]
        public void CsgBooleanResultTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(CsgBooleanResultTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                solids.Count.Should().Be(2);

            }

        }
        /// <summary>
        /// Tests if a boolean processes correctly if not it will silent fail and the test should fail
        /// </summary>
        [Fact]
        public void BooleanSilentFailTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(BooleanSilentFailTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }

        [Fact]
        public void polygonally_bounded_half_space_clip()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(polygonally_bounded_half_space_clip)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }

        [Fact]
        public void unstable_boolean_clipping_result()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(unstable_boolean_clipping_result)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }

        /// <summary>
        /// This problem is a boolean where the tolerance needs to be made courser by 10 fold
        /// </summary>
        [Fact]
        public void BadlyOrientedBrepFacesTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(BadlyOrientedBrepFacesTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }
        /// <summary>
        /// This problem is a boolean where the tolerance needs to be made courser by 10 fold
        /// </summary>
        [Fact]
        public void FaceWithBoundsOutsideDeclaredPrecisionTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(FaceWithBoundsOutsideDeclaredPrecisionTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }

        [Fact]
        public void UnorderedCompositeCurveTest()
        {
            using (var er = new EntityRepository<IIfcCompositeCurve>(nameof(UnorderedCompositeCurveTest), 1, 1e-5, true))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var wire = geomEngine.CreateWire(er.Entity, logger);
                wire.Should().NotBeNull();
                wire.IsValid.Should().BeTrue();

            }

        }

        [Fact]
        public void BooleanResultTimoutTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(BooleanResultTimoutTest), 1, 1e-5, false))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }
        [Fact]
        public void BooleanResultTimout2Test()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>("boolean_result_timing_out2"))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }
        [Fact]
        public void ComplexNestedBooleanResult()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(ComplexNestedBooleanResult), 1, 1e-5, true))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }
        [Fact]
        public void VerySmallBooleanCutTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(VerySmallBooleanCutTest), 1, 1e-5, false))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }
        [Fact]
        public void FailingBooleanBrepWithZeroVolumeTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(FailingBooleanBrepWithZeroVolumeTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                HelperFunctions.IsValidSolid(solids.FirstOrDefault());

            }

        }
        /// <summary>
        /// This test subtracts a cuboid formed from a closed shell from a cuboid formed from and extruded area solid, the two are identical
        /// </summary>
        [Fact]
        public void EmptyBooleanResultTest()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(EmptyBooleanResultTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                solids.Count.Should().Be(0, "No solids should be produced");
            }

        }
        /// <summary>
        /// Just cuts two extruded area solids from each other to leave a cuboid
        /// </summary>
        [Fact]
        public void SimpleBooleanClipResultTest()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(SimpleBooleanClipResultTest)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solid = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                solid.Faces.Count.Should().Be(6, "This solid should have 6 faces");
            }

        }



        /// <summary>
        /// Cuts one cylinder from another and returns a valid solid
        /// </summary>
        [Fact]
        public void CutTwoCylindersTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {

                    var cylinderInner = IfcModelBuilder.MakeRightCircularCylinder(m, 10, 20);
                    var cylinderOuter = IfcModelBuilder.MakeRightCircularCylinder(m, 20, 20);
                    var logger = new NullLogger<IfcAdvancedBrepTests>();
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var outer = geomEngine.CreateSolid(cylinderOuter, logger);
                    var inner = geomEngine.CreateSolid(cylinderInner, logger);
                    var solidSet = outer.Cut(inner, m.ModelFactors.PrecisionBoolean);

                    solidSet.Count.Should().Be(1, "Cutting these two solids should return a single solid");
                    IsSolidTest(solidSet.First);
                    txn.Commit();
                }
            }
        }

        /// <summary>
        /// Unions a sphere and a cylinder
        /// </summary>
        [Fact]
        public void BooleanUnionSolidTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var cylinder = IfcModelBuilder.MakeRightCircularCylinder(m, 10, 20);
                    var sphere = IfcModelBuilder.MakeSphere(m, 15);
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var a = geomEngine.CreateSolid(sphere, logger);
                    var b = geomEngine.CreateSolid(cylinder, logger);
                    var solidSet = a.Union(b, m.ModelFactors.PrecisionBoolean);
                    solidSet.Count.Should().Be(1, "unioning these two solids should return a single solid");
                    IsSolidTest(solidSet.First);
                }
            }
        }

        /// <summary>
        /// Intersects a cylinder and a sphere
        /// </summary>
        [Fact]
        public void BooleanIntersectSolidTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var cylinder = IfcModelBuilder.MakeRightCircularCylinder(m, 10, 20);
                    var sphere = IfcModelBuilder.MakeSphere(m, 15);
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var a = geomEngine.CreateSolid(sphere, logger);
                    var b = geomEngine.CreateSolid(cylinder, logger);
                    var solidSet = a.Intersection(b, m.ModelFactors.PrecisionBoolean);
                    solidSet.Count.Should().Be(1, "intersecting these two solids should return a single solid");
                    IsSolidTest(solidSet.First);
                }
            }
        }


        [Fact]
        public void SectionOfCylinderTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var cylinder = IfcModelBuilder.MakeRightCircularCylinder(m, 20, 20);
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var solid = geomEngine.CreateSolid(cylinder, logger);
                    var plane = IfcModelBuilder.MakePlane(m, new XbimPoint3D(cylinder.Position.Location.X + 1, cylinder.Position.Location.Y, cylinder.Position.Location.Z), new XbimVector3D(0, -1, 0), new XbimVector3D(1, 0, 0));
                    var cutPlane = geomEngine.CreateFace(plane, logger);
                    var section = solid.Section(cutPlane, m.ModelFactors.PrecisionBoolean);
                    section.First.Should().NotBeNull();
                    section.First.OuterBound.Edges.Count.Should().Be(4, "4 edges are required for this section of a cylinder");
                    //repeat with section through cylinder
                    plane = IfcModelBuilder.MakePlane(m, new XbimPoint3D(cylinder.Position.Location.X + 1, cylinder.Position.Location.Y, cylinder.Position.Location.Z), new XbimVector3D(0, 0, 1), new XbimVector3D(0, 1, 0));
                    cutPlane = geomEngine.CreateFace(plane, logger);
                    section = solid.Section(cutPlane, m.ModelFactors.PrecisionBoolean);
                    section.First.Should().NotBeNull();
                    section.First.OuterBound.Edges.Count.Should().Be(1);
                    section.First.InnerBounds.Count.Should().Be(0);

                }
            }
        }

        [Fact]
        public void SectionWithInnerWireTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {

                    var csgTree = m.Instances.New<IfcCsgSolid>();
                    var bresult = m.Instances.New<IfcBooleanResult>();
                    var cylinderInner = IfcModelBuilder.MakeRightCircularCylinder(m, 10, 20);
                    var cylinderOuter = IfcModelBuilder.MakeRightCircularCylinder(m, 20, 20);
                    bresult.FirstOperand = cylinderOuter;
                    bresult.SecondOperand = cylinderInner;
                    bresult.Operator = IfcBooleanOperator.DIFFERENCE;
                    csgTree.TreeRootExpression = bresult;
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var solid = geomEngine.CreateSolidSet(csgTree, logger).FirstOrDefault();
                    var plane = IfcModelBuilder.MakePlane(m, new XbimPoint3D(cylinderInner.Position.Location.X + 1, cylinderInner.Position.Location.Y, cylinderInner.Position.Location.Z), new XbimVector3D(0, 0, 1), new XbimVector3D(0, 1, 0));
                    var cutPlane = geomEngine.CreateFace(plane, logger);
                    var section = solid.Section(cutPlane, m.ModelFactors.PrecisionBoolean);
                    section.First.Should().NotBeNull();
                    section.First.OuterBound.Edges.Count.Should().Be(1);
                    section.First.InnerBounds.Count.Should().Be(1);

                }
            }
        }

        [Fact]
        public void SectionOfBlockTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var block = IfcModelBuilder.MakeBlock(m, 10, 15, 20);
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var solid = geomEngine.CreateSolid(block, logger);
                    var plane = IfcModelBuilder.MakePlane(m, new XbimPoint3D(block.Position.Location.X + 5, block.Position.Location.Y, block.Position.Location.Z), new XbimVector3D(-1, 0, 0), new XbimVector3D(0, 1, 0));
                    var cutPlane = geomEngine.CreateFace(plane, logger);

                    var section = solid.Section(cutPlane, m.ModelFactors.PrecisionBoolean);
                    section.First.Should().NotBeNull();
                    section.First.OuterBound.Edges.Count.Should().Be(4);

                }
            }
        }


        [Fact]
        public void IfcCsgDifferenceTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var csgTree = m.Instances.New<IfcCsgSolid>();
                    var bresult = m.Instances.New<IfcBooleanResult>();

                    var cylinderInner = IfcModelBuilder.MakeRightCircularCylinder(m, 10, 20);
                    var cylinderOuter = IfcModelBuilder.MakeRightCircularCylinder(m, 20, 20);
                    bresult.FirstOperand = cylinderOuter;
                    bresult.SecondOperand = cylinderInner;
                    bresult.Operator = IfcBooleanOperator.DIFFERENCE;
                    csgTree.TreeRootExpression = bresult;
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var solid = geomEngine.CreateSolidSet(csgTree, logger).FirstOrDefault();
                    solid.Faces.Count.Should().Be(4);
                    solid.Vertices.Count.Should().Be(4);

                }
            }
        }
        [Fact]
        public void CSG_with_self_intersecting_wire_test()
        {
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(CSG_with_self_intersecting_wire_test)))
            {
                er.Entity.Should().NotBeNull();
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solids = geomEngine.CreateSolidSet(er.Entity, logger);
                solids.Count.Should().Be(1);
            }
        }


        [Fact]
        public void IfcCsgIntersectionTest()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var csgTree = m.Instances.New<IfcCsgSolid>();
                    var bresult = m.Instances.New<IfcBooleanResult>();

                    var cylinder = IfcModelBuilder.MakeRightCircularCylinder(m, 10, 20);
                    var sphere = IfcModelBuilder.MakeSphere(m, 15);
                    bresult.FirstOperand = cylinder;
                    bresult.SecondOperand = sphere;
                    bresult.Operator = IfcBooleanOperator.INTERSECTION;
                    csgTree.TreeRootExpression = bresult;
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var solid = geomEngine.CreateSolidSet(csgTree, logger).FirstOrDefault();
                    solid.Faces.Count.Should().Be(3);
                    solid.Vertices.Count.Should().Be(3);

                }
            }
        }

        [Fact]
        public void IfcHalfspace_Test()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var halfSpace = m.Instances.New<IfcHalfSpaceSolid>();
                    halfSpace.AgreementFlag = false;
                    var baseSurface = m.Instances.New<IfcPlane>();
                    baseSurface.Position = m.Instances.New<IfcAxis2Placement3D>();
                    baseSurface.Position.Location = m.Instances.New<IfcCartesianPoint>();
                    baseSurface.Position.Location.X = 0;
                    baseSurface.Position.Location.Y = 0;
                    baseSurface.Position.Location.Z = 10;
                    halfSpace.BaseSurface = baseSurface;
                    //make an extrusion
                    var profile = IfcModelBuilder.MakeRectangleHollowProfileDef(m, 20, 10, 1);
                    var extrude = IfcModelBuilder.MakeExtrudedAreaSolid(m, profile, 40);
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var solid = geomEngine.CreateSolid(extrude, logger);
                    var halfSpaceSolid = geomEngine.CreateSolid(halfSpace, logger);
                    var cut = solid.Cut(halfSpaceSolid, 1e-5);
                    cut.Count.Should().BeGreaterThan(0);
                    Math.Abs((solid.Volume * .25) - cut.First.Volume).Should().BeLessThan(1e-5);
                    //move the halfspace plane up
                    baseSurface.Position.Location.Z = 30;
                    halfSpaceSolid = geomEngine.CreateSolid(halfSpace, logger);
                    cut = solid.Cut(halfSpaceSolid, 1e-5);
                    Math.Abs((solid.Volume * .75) - cut.First.Volume).Should().BeLessThan(1e-5);
                    //reverse halfspace agreement
                    halfSpace.AgreementFlag = true;
                    halfSpaceSolid = geomEngine.CreateSolid(halfSpace, logger);
                    cut = solid.Cut(halfSpaceSolid, 1e-5);
                    Math.Abs((solid.Volume * .25) - cut.First.Volume).Should().BeLessThan(1e-5);

                }
            }
        }

        [Fact]
        public void IfcPolygonalBoundedHalfspace_Test()
        {
            using (var m = new MemoryModel(new Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction(""))
                {
                    var polygonalBoundedHalfspace = m.Instances.New<IfcPolygonalBoundedHalfSpace>();
                    polygonalBoundedHalfspace.AgreementFlag = false;
                    var plane = m.Instances.New<IfcPlane>();
                    plane.Position = m.Instances.New<IfcAxis2Placement3D>();
                    plane.Position.Location = m.Instances.New<IfcCartesianPoint>(c => c.SetXYZ(0, 0, 0));

                    polygonalBoundedHalfspace.BaseSurface = plane;
                    //create the polygonal bound
                    var polyLine = m.Instances.New<IfcPolyline>();
                    polyLine.Points.Add(m.Instances.New<IfcCartesianPoint>(c => c.SetXY(0, 2.5)));
                    polyLine.Points.Add(m.Instances.New<IfcCartesianPoint>(c => c.SetXY(5, 2.5)));
                    polyLine.Points.Add(m.Instances.New<IfcCartesianPoint>(c => c.SetXY(5, -2.5)));
                    polyLine.Points.Add(m.Instances.New<IfcCartesianPoint>(c => c.SetXY(0, -2.5)));
                    polyLine.Points.Add(m.Instances.New<IfcCartesianPoint>(c => c.SetXY(0, 2.5)));
                    polygonalBoundedHalfspace.PolygonalBoundary = polyLine;

                    var basePos = m.Instances.New<IfcAxis2Placement3D>();
                    basePos.Location = m.Instances.New<IfcCartesianPoint>(c => c.SetXYZ(0, 0, 0));
                    polygonalBoundedHalfspace.Position = basePos;
                    //make an extrusion
                    var profile = IfcModelBuilder.MakeRectangleProfileDef(m, 20, 10);
                    var extrude = IfcModelBuilder.MakeExtrudedAreaSolid(m, profile, 40);
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var solid = geomEngine.CreateSolid(extrude, logger);
                    var halfSpaceSolid = geomEngine.CreateSolid(polygonalBoundedHalfspace, logger);
                    var cut = solid.Cut(halfSpaceSolid, 1e-5);

                    cut.Count.Should().BeGreaterThan(0);
                    Math.Abs((solid.Volume) - cut.First.Volume - 1000).Should().BeLessThan(1e-5);

                    //reverse halfspace agreement
                    polygonalBoundedHalfspace.AgreementFlag = true;
                    halfSpaceSolid = geomEngine.CreateSolid(polygonalBoundedHalfspace, logger);
                    cut = solid.Cut(halfSpaceSolid, 1e-5);
                    Math.Abs(solid.Volume - cut.First.Volume).Should().BeLessThan(1e-5);

                    //move the plane up
                    plane.Position.Location.Z = 20;
                    halfSpaceSolid = geomEngine.CreateSolid(polygonalBoundedHalfspace, logger);
                    cut = solid.Cut(halfSpaceSolid, 1e-5);
                    Math.Abs(solid.Volume - cut.First.Volume - 500).Should().BeLessThan(1e-5);

                    //some realistic data
                    polyLine.Points[0].SetXY(0, 0);
                    polyLine.Points[1].SetXY(0, 2850);
                    polyLine.Points[2].SetXY(-350, 2850);
                    polyLine.Points[3].SetXY(-350, 0);
                    polyLine.Points[4].SetXY(0, 0);
                    plane.Position.Location.SetXYZ(-5240.7742616303667, -33052.9790707385, 0.0);
                    plane.Position.Axis = m.Instances.New<IfcDirection>(d => d.SetXYZ(0, -1, 0));
                    plane.Position.RefDirection = m.Instances.New<IfcDirection>(d => d.SetXYZ(1, 0, 0));
                    basePos.Location.SetXYZ(-5240.7742616303667, -33052.9790707385, 0);
                    basePos.Axis = plane.Position.Axis;
                    basePos.RefDirection = plane.Position.RefDirection;

                    halfSpaceSolid = geomEngine.CreateSolid(polygonalBoundedHalfspace, logger);

                    profile.XDim = 350;
                    profile.YDim = 125;
                    profile.Position.Location.SetXY(-5415.7742616303676, -32932.529070738507);
                    extrude.Depth = 2850;
                    solid = geomEngine.CreateSolid(extrude, logger);

                    cut = solid.Cut(halfSpaceSolid, 1e-5); //everything should be cut
                    cut.Count.Should().Be(0);

                }
            }
        }


        [Fact]
        public void IfcHalfspaceCutFromIfcExtrudedAreaSolidTest()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(IfcHalfspaceCutFromIfcExtrudedAreaSolidTest)))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solid = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                IsSolidTest(solid);
                solid.Faces.Count().Should().Be(6);
            }
        }


        [Fact]
        public void IfcPolygonalBoundedHalfspaceCutFromIfcExtrudedAreaSolidTest()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(IfcPolygonalBoundedHalfspaceCutFromIfcExtrudedAreaSolidTest)))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solid = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                IsSolidTest(solid);
                solid.Faces.Count().Should().Be(6);
            }
        }

        [Fact]
        public void NestedBooleansTest()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(NestedBooleansTest)))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solid = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                IsSolidTest(solid);
                solid.Faces.Count().Should().Be(6);
            }
        }

        [Fact]
        public void NestedBooleanClippingResultsTest()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(NestedBooleanClippingResultsTest)))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solid = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                IsSolidTest(solid);
                solid.Faces.Count().Should().Be(7);
            }
        }

        //this test is 2 boolean clipping on efffectivel a beam, but the second cut is an illegal solid with coincidental faces
        //this test checks that the booleans do the right thing
        [Fact]
        public void SmallBooleanClippingResultsTest()
        {
            IXbimSolid solidBody, solidCut1, solidCut2, solidResult;
            // this is a simple cuboid / beam
            using (var er = new EntityRepository<IIfcExtrudedAreaSolid>("SmallBooleanClippingResultsTestBodyShape"))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                solidBody = geomEngine.CreateSolid(er.Entity, logger);
                solidBody.Faces.Count.Should().Be(6);
            }
            //this is a triangular fillet (prism) to cut off the sort side face
            using (var er = new EntityRepository<IIfcFacetedBrep>("SmallBooleanClippingResultsTestCutShape1"))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                solidCut1 = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                solidCut1.Faces.Count.Should().Be(5);
            }
            // this shape is a faulty solid
            using (var er = new EntityRepository<IIfcFacetedBrep>("SmallBooleanClippingResultsTestCutShape2"))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                solidCut2 = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                solidCut2.Faces.Count.Should().Be(10); ;
            }
            using (var er = new EntityRepository<IIfcBooleanResult>(nameof(SmallBooleanClippingResultsTest)))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                solidResult = geomEngine.CreateSolidSet(er.Entity, logger).FirstOrDefault();
                var actualVolume = solidResult.Volume;
                solidBody.Volume.Should().BeGreaterThan(actualVolume, "This cut solid should have less volume than the body shape");
                solidResult.Faces.Count.Should().Be(8, "This solid should have 8 faces");
            }
        }
        [Fact]
        public void EmptyBooleanClippingResultTest()
        {
            using (var er = new EntityRepository<IIfcBooleanClippingResult>(nameof(EmptyBooleanClippingResultTest)))
            {
                var geomEngine = new XbimGeometryEngine(er.Entity.Model, loggerFactory);
                var solidSet = geomEngine.CreateSolidSet(er.Entity, logger);
                solidSet.Count.Should().Be(0); ;
            }
        }

        [Fact]
        public void IfcBooleanClippingResult()
        {
            using (var model = MemoryModel.OpenRead(@".\TestFiles\IfcWallWithIfcBooleanClippingResult1.ifc"))
            {
                var geomEngine = new XbimGeometryEngine(model, loggerFactory);
                var wallBCR = model.Instances[34] as IIfcBooleanClippingResult;
                var solidSet = geomEngine.CreateSolidSet(wallBCR, null);
                solidSet.Sum(s => s.Faces.Count).Should().Be(14);
            }

            using (var model = MemoryModel.OpenRead(@".\TestFiles\IfcWallWithIfcBooleanClippingResult2.ifc"))
            {
                var geomEngine = new XbimGeometryEngine(model, loggerFactory);
                var wallBCR = model.Instances[40] as IIfcBooleanClippingResult;
                var solidSet = geomEngine.CreateSolidSet(wallBCR, null);
                solidSet.Sum(s => s.Faces.Count).Should().Be(8);
            }
        }

        [Fact]
        public void CuttingOpeningInCompositeProfileDefTest()
        {
            using (var repos = new EntityRepository<IIfcRelVoidsElement>("CuttingOpeningInCompositeProfileDefTest"))
            {

                var relVoids = repos.Entity;
                var oneMilli = relVoids.Model.ModelFactors.OneMilliMetre;
                var precision = oneMilli / 10;
                var wall = relVoids.RelatingBuildingElement;
                var wallPlacement = wall.ObjectPlacement as IIfcLocalPlacement;
                var wallTransform = wallPlacement.ToMatrix3D();
                var geomEngine = new XbimGeometryEngine(repos.Entity.Model, loggerFactory);
                var wallTransform2 = geomEngine.ToMatrix3D(wallPlacement, logger);
                wallTransform.Should().BeEquivalentTo(wallTransform2);
                var wallGeom = wall.Representation.Representations.FirstOrDefault().Items.FirstOrDefault() as IIfcExtrudedAreaSolid;
                var opening = relVoids.RelatedOpeningElement;
                var openingPlacement = opening.ObjectPlacement as IIfcLocalPlacement;
                var openingGeoms = opening.Representation.Representations.FirstOrDefault().Items.OfType<IIfcExtrudedAreaSolid>().ToList();
                var openingTransform = openingPlacement.ToMatrix3D(); ;
                var wallBrep = geomEngine.CreateSolidSet(wallGeom);
                var wallBrepPlaced = wallBrep.Transform(wallTransform) as IXbimSolidSet;

                var openingBReps = geomEngine.CreateSolidSet();

                foreach (var og in openingGeoms)
                {
                    var brep = geomEngine.CreateSolid(og, logger);
                    openingBReps.Add(brep.Transform(openingTransform) as IXbimSolid);
                }
                int uncut = 0;
                var singleCut = wallBrepPlaced.Cut(openingBReps, precision);
                var vol = 0.0;
                foreach (var uncutItem in wallBrepPlaced)
                {
                    var result = uncutItem.Cut(openingBReps, precision);
                    result.Count.Should().Be(1);
                    var cutSolid = result.First as IXbimSolid;
                    cutSolid.Should().NotBeNull();
                    cutSolid.IsValid.Should().BeTrue();
                    if (uncutItem.Volume <= cutSolid.Volume) uncut++;
                    uncut.Should().BeLessThanOrEqualTo(3);
                    vol += cutSolid.Volume;
                }
                uncut.Should().Be(2);
                var scutVol = singleCut.Sum(s => s.Volume);
                Math.Abs(vol - scutVol).Should().BeLessThan(1e-5);


            }
        }

        [Fact]
        public void CuttingOpeningInIfcFaceBasedSurfaceModelTest()
        {
            using (var bodyEntity = new EntityRepository<IIfcFaceBasedSurfaceModel>("CuttingOpeningInIfcFaceBasedSurfaceModelBodyTest"))
            {
                using (var holeEntity = new EntityRepository<IIfcExtrudedAreaSolid>("CuttingOpeningInIfcFaceBasedSurfaceModelVoidTest"))
                {
                    var geomEngine = new XbimGeometryEngine(bodyEntity.Entity.Model, loggerFactory);
                    var body = geomEngine.CreateSolidSet(bodyEntity.Entity, logger);
                    body.Count.Should().Be(8);
                    geomEngine = new XbimGeometryEngine(holeEntity.Entity.Model, loggerFactory);
                    var hole = geomEngine.CreateSolid(holeEntity.Entity, logger);
                    var result = body.Cut(hole, bodyEntity.Entity.Model.ModelFactors.Precision);

                    result.Count.Should().Be(8, "Eight solids should be returned");
                    foreach (var solid in result)
                    {
                        IsSolidTest(solid);
                    }

                }

            }
        }
        //[Fact]
        //public void Can_cut_polygonal_faceset_solids()
        //{
        //    using (var model = MemoryModel.OpenRead(@".\TestFiles\Ifc4TestFiles\Can_cut_polygonal_faceset_solids.ifc"))
        //    {
        //        //get the geometry of the wall, it is made of 4 items
        //        var partTransform = (model.Instances[18] as IIfcLocalPlacement).ToMatrix3D();
        //        var part1 = geomEngine.Create(model.Instances[47] as IIfcPolygonalFaceSet, logger)
        //            .Transform(partTransform);
        //        var part2 = geomEngine.Create(model.Instances[59] as IIfcPolygonalFaceSet, logger)
        //            .Transform(partTransform); ;
        //        var part3 = geomEngine.Create(model.Instances[71] as IIfcPolygonalFaceSet, logger)
        //            .Transform(partTransform); ;
        //        var part4 = geomEngine.Create(model.Instances[83] as IIfcPolygonalFaceSet, logger)
        //            .Transform(partTransform); ;

        //        var opening1 = geomEngine.Create(model.Instances[105] as IIfcPolygonalFaceSet, logger)
        //            .Transform((model.Instances[98] as IIfcLocalPlacement).ToMatrix3D());
        //        var opening2 = geomEngine.Create(model.Instances[130] as IIfcPolygonalFaceSet, logger)
        //            .Transform((model.Instances[123] as IIfcLocalPlacement).ToMatrix3D());
        //        var opening3 = geomEngine.Create(model.Instances[155] as IIfcPolygonalFaceSet, logger)
        //            .Transform((model.Instances[148] as IIfcLocalPlacement).ToMatrix3D());

        //        var body = geomEngine.CreateSolidSet();
        //        body.Add(part1);
        //        body.Add(part2);
        //       // body.Add(part3);
        //       // body.Add(part4);
        //        var openings = geomEngine.CreateSolidSet();
        //        openings.Add(opening1);
        //        //openings.Add(opening2);
        //        //openings.Add(opening3);
        //        var result = body.Cut(openings,model.ModelFactors.Precision,logger);
        //    }

        //}
        #region Solid with voids test
        [Fact]
        public void BooleanCutSolidWithVoidPlanarTest()
        {
            using (var m = new MemoryModel(new Xbim.Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction("Populate"))
                {

                    var block1 = IfcModelBuilder.MakeBlock(m, 20, 20, 20);
                    var block2 = IfcModelBuilder.MakeBlock(m, 5, 5, 5);
                    block2.Position.Location.X += 10;
                    block2.Position.Location.Y += 10;
                    block2.Position.Location.Z += 10;
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var b1 = geomEngine.CreateSolid(block1, logger);
                    var b2 = geomEngine.CreateSolid(block2, logger);
                    var result = b1.Cut(b2, m.ModelFactors.PrecisionBoolean);
                    result.Count.Should().Be(1);
                    foreach (var solid in result)
                        HelperFunctions.IsValidSolid(solid);
                    txn.Commit();
                }
            }
        }

        [Fact]
        public void BooleanCutSolidWithVoidNonPlanarTest()
        {
            using (var m = new MemoryModel(new Xbim.Ifc4.EntityFactoryIfc4()))
            {
                using (var txn = m.BeginTransaction("Populate"))
                {

                    var block1 = IfcModelBuilder.MakeSphere(m, 20);
                    var block2 = IfcModelBuilder.MakeSphere(m, 5);
                    var geomEngine = new XbimGeometryEngine(m, loggerFactory);
                    var b1 = geomEngine.CreateSolid(block1, logger);
                    var b2 = geomEngine.CreateSolid(block2, logger);
                    var result = b1.Cut(b2, m.ModelFactors.PrecisionBoolean);
                    result.Count.Should().Be(1, "Cutting of these two solids should return two  solids");
                    const double vOuter = (4.0 / 3.0) * Math.PI * 20.0 * 20.0 * 20.0;
                    const double vInner = (4.0 / 3.0) * Math.PI * 5.0 * 5.0 * 5.0;
                    const double volume = vOuter - vInner;

                    (result.First.Volume - volume).Should().BeLessThanOrEqualTo(m.ModelFactors.Precision, "Volume is incorrect");
                    txn.Commit();
                }
            }
        }
        #endregion

    }
}
